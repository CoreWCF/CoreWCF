using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using CoreWCF.Runtime;
using CoreWCF.Runtime.Diagnostics;
using CoreWCF;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    sealed class ConnectionDemuxer : IDisposable
    {
        ConnectionAcceptor acceptor;

        // we use this list to track readers that don't have a clear owner (so they don't get GC'ed)
        List<InitialServerConnectionReader> connectionReaders;

        bool isDisposed;
        ConnectionModeCallback onConnectionModeKnown;
        ConnectionModeCallback onCachedConnectionModeKnown;
        ConnectionClosedCallback onConnectionClosed;
        ServerSessionPreambleCallback onSessionPreambleKnown;
        ServerSingletonPreambleCallback onSingletonPreambleKnown;
        Action<object> reuseConnectionCallback;
        ServerSessionPreambleDemuxCallback serverSessionPreambleCallback;
        SingletonPreambleDemuxCallback singletonPreambleCallback;
        TransportSettingsCallback transportSettingsCallback;
        Action pooledConnectionDequeuedCallback;
        Action<Uri> viaDelegate;
        TimeSpan channelInitializationTimeout;
        TimeSpan idleTimeout;
        int maxPooledConnections;
        int pooledConnectionCount;

        public ConnectionDemuxer(IConnectionListener listener, int maxAccepts, int maxPendingConnections,
            TimeSpan channelInitializationTimeout, TimeSpan idleTimeout, int maxPooledConnections,
            TransportSettingsCallback transportSettingsCallback,
            SingletonPreambleDemuxCallback singletonPreambleCallback,
            ServerSessionPreambleDemuxCallback serverSessionPreambleCallback, ErrorCallback errorCallback)
        {
            connectionReaders = new List<InitialServerConnectionReader>();
            acceptor =
                new ConnectionAcceptor(listener, maxAccepts, maxPendingConnections, OnConnectionAvailable, errorCallback);
            this.channelInitializationTimeout = channelInitializationTimeout;
            this.idleTimeout = idleTimeout;
            this.maxPooledConnections = maxPooledConnections;
            onConnectionClosed = new ConnectionClosedCallback(OnConnectionClosed);
            this.transportSettingsCallback = transportSettingsCallback;
            this.singletonPreambleCallback = singletonPreambleCallback;
            this.serverSessionPreambleCallback = serverSessionPreambleCallback;
        }

        object ThisLock
        {
            get { return this; }
        }

        public void Dispose()
        {
            lock (ThisLock)
            {
                if (isDisposed)
                    return;

                isDisposed = true;
            }

            for (int i = 0; i < connectionReaders.Count; i++)
            {
                connectionReaders[i].Dispose();
            }
            connectionReaders.Clear();

            acceptor.Dispose();
        }

        ConnectionModeReader SetupModeReader(IConnection connection, bool isCached)
        {
            ConnectionModeReader modeReader;
            if (isCached)
            {
                if (onCachedConnectionModeKnown == null)
                {
                    onCachedConnectionModeKnown = new ConnectionModeCallback(OnCachedConnectionModeKnown);
                }

                modeReader = new ConnectionModeReader(connection, onCachedConnectionModeKnown, onConnectionClosed);
            }
            else
            {
                if (onConnectionModeKnown == null)
                {
                    onConnectionModeKnown = new ConnectionModeCallback(OnConnectionModeKnown);
                }

                modeReader = new ConnectionModeReader(connection, onConnectionModeKnown, onConnectionClosed);
            }

            lock (ThisLock)
            {
                if (isDisposed)
                {
                    modeReader.Dispose();
                    return null;
                }

                connectionReaders.Add(modeReader);
                return modeReader;
            }
        }

        public void ReuseConnection(IConnection connection, TimeSpan closeTimeout)
        {
            connection.ExceptionEventType = TraceEventType.Information;
            ConnectionModeReader modeReader = SetupModeReader(connection, true);

            if (modeReader != null)
            {
                if (reuseConnectionCallback == null)
                {
                    reuseConnectionCallback = new Action<object>(ReuseConnectionCallback);
                }

                ActionItem.Schedule(reuseConnectionCallback, new ReuseConnectionState(modeReader, closeTimeout));
            }
        }

        void ReuseConnectionCallback(object state)
        {
            ReuseConnectionState connectionState = (ReuseConnectionState)state;
            bool closeReader = false;
            lock (ThisLock)
            {
                if (pooledConnectionCount >= maxPooledConnections)
                {
                    closeReader = true;
                }
                else
                {
                    pooledConnectionCount++;
                }
            }

            if (closeReader)
            {
                connectionState.ModeReader.CloseFromPool(connectionState.CloseTimeout);
            }
            else
            {
                if (pooledConnectionDequeuedCallback == null)
                {
                    pooledConnectionDequeuedCallback = new Action(PooledConnectionDequeuedCallback);
                }
                connectionState.ModeReader.StartReading(idleTimeout, pooledConnectionDequeuedCallback);
            }
        }

        void PooledConnectionDequeuedCallback()
        {
            lock (ThisLock)
            {
                pooledConnectionCount--;
                Fx.Assert(pooledConnectionCount >= 0, "Connection Throttle should never be negative");
            }
        }

        void OnConnectionAvailable(IConnection connection, Action connectionDequeuedCallback)
        {
            ConnectionModeReader modeReader = SetupModeReader(connection, false);

            if (modeReader != null)
            {
                // StartReading() will never throw non-fatal exceptions; 
                // it propagates all exceptions into the onConnectionModeKnown callback, 
                // which is where we need our robust handling
                modeReader.StartReading(channelInitializationTimeout, connectionDequeuedCallback);
            }
            else
            {
                connectionDequeuedCallback();
            }
        }

        void OnCachedConnectionModeKnown(ConnectionModeReader modeReader)
        {
            OnConnectionModeKnownCore(modeReader, true);
        }

        void OnConnectionModeKnown(ConnectionModeReader modeReader)
        {
            OnConnectionModeKnownCore(modeReader, false);
        }

        void OnConnectionModeKnownCore(ConnectionModeReader modeReader, bool isCached)
        {
            lock (ThisLock)
            {
                if (isDisposed)
                    return;

                connectionReaders.Remove(modeReader);
            }

            bool closeReader = true;
            try
            {
                FramingMode framingMode;
                try
                {
                    framingMode = modeReader.GetConnectionMode();
                }
                catch (CommunicationException exception)
                {
                    TraceEventType eventType = modeReader.Connection.ExceptionEventType;
                    DiagnosticUtility.TraceHandledException(exception, eventType);
                    return;
                }
                catch (TimeoutException exception)
                {
                    if (!isCached)
                    {
                        exception = new TimeoutException(SR.Format(SR.ChannelInitializationTimeout, channelInitializationTimeout), exception);
                        ErrorBehaviorHelper.ThrowAndCatch(exception);
                    }

                    TraceEventType eventType = modeReader.Connection.ExceptionEventType;
                    DiagnosticUtility.TraceHandledException(exception, eventType);
                    return;
                }

                switch (framingMode)
                {
                    case FramingMode.Duplex:
                        OnDuplexConnection(modeReader.Connection, modeReader.ConnectionDequeuedCallback,
                            modeReader.StreamPosition, modeReader.BufferOffset, modeReader.BufferSize,
                            modeReader.GetRemainingTimeout());
                        break;

                    case FramingMode.Singleton:
                        OnSingletonConnection(modeReader.Connection, modeReader.ConnectionDequeuedCallback,
                            modeReader.StreamPosition, modeReader.BufferOffset, modeReader.BufferSize,
                            modeReader.GetRemainingTimeout());
                        break;

                    default:
                        {
                            Exception inner = new InvalidDataException(SR.Format(
                                SR.FramingModeNotSupported, framingMode));
                            Exception exception = new ProtocolException(inner.Message, inner);
                            FramingEncodingString.AddFaultString(exception, FramingEncodingString.UnsupportedModeFault);
                            ErrorBehaviorHelper.ThrowAndCatch(exception);
                            return;
                        }
                }

                closeReader = false;
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                if (!ExceptionHandlerHelper.HandleTransportExceptionHelper(e))
                {
                    throw;
                }

                // containment -- the reader is aborted, no need for additional containment
            }
            finally
            {
                if (closeReader)
                {
                    modeReader.Dispose();
                }
            }
        }

        void OnConnectionClosed(InitialServerConnectionReader connectionReader)
        {
            lock (ThisLock)
            {
                if (isDisposed)
                    return;

                connectionReaders.Remove(connectionReader);
            }
        }

        void OnSingletonConnection(IConnection connection, Action connectionDequeuedCallback,
            long streamPosition, int offset, int size, TimeSpan timeout)
        {
            if (onSingletonPreambleKnown == null)
            {
                onSingletonPreambleKnown = OnSingletonPreambleKnown;
            }
            ServerSingletonPreambleConnectionReader singletonPreambleReader =
                new ServerSingletonPreambleConnectionReader(connection, connectionDequeuedCallback, streamPosition, offset, size,
                transportSettingsCallback, onConnectionClosed, onSingletonPreambleKnown);

            lock (ThisLock)
            {
                if (isDisposed)
                {
                    singletonPreambleReader.Dispose();
                    return;
                }

                connectionReaders.Add(singletonPreambleReader);
            }
            //TODO: This might block a thread. Work out if it's safe to make this method async void
            //      or make the caller async.
            singletonPreambleReader.StartReadingAsync(viaDelegate, timeout).GetAwaiter().GetResult();
        }

        void OnSingletonPreambleKnown(ServerSingletonPreambleConnectionReader serverSingletonPreambleReader)
        {
            lock (ThisLock)
            {
                if (isDisposed)
                {
                    return;
                }

                connectionReaders.Remove(serverSingletonPreambleReader);
            }

            ISingletonChannelListener singletonChannelListener = singletonPreambleCallback(serverSingletonPreambleReader);
            Fx.Assert(singletonChannelListener != null,
                "singletonPreambleCallback must return a listener or send a Fault/throw");

            // transfer ownership of the connection from the preamble reader to the message handler

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            CompleteSingletonPreambleAsync(serverSingletonPreambleReader, singletonChannelListener);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
        }

        async Task CompleteSingletonPreambleAsync(ServerSingletonPreambleConnectionReader serverSingletonPreambleReader, ISingletonChannelListener singletonChannelListener)
        {
            var timeoutHelper = new TimeoutHelper(singletonChannelListener.ReceiveTimeout);
            IConnection upgradedConnection = await serverSingletonPreambleReader.CompletePreambleAsync(singletonChannelListener.ReceiveTimeout);
            ServerSingletonConnectionReader singletonReader = new ServerSingletonConnectionReader(serverSingletonPreambleReader, upgradedConnection, this);

            //singletonReader doesn't have async version of ReceiveRequest, so just call the sync method for now.
            RequestContext requestContext = await singletonReader.ReceiveRequestAsync(timeoutHelper.GetCancellationToken());
            singletonChannelListener.ReceiveRequest(requestContext, serverSingletonPreambleReader.ConnectionDequeuedCallback, true);

        }

        void OnSessionPreambleKnown(ServerSessionPreambleConnectionReader serverSessionPreambleReader)
        {
            lock (ThisLock)
            {
                if (isDisposed)
                {
                    return;
                }

                connectionReaders.Remove(serverSessionPreambleReader);
            }

            TraceOnSessionPreambleKnown(serverSessionPreambleReader);

            serverSessionPreambleCallback(serverSessionPreambleReader, this);
        }

        static void TraceOnSessionPreambleKnown(ServerSessionPreambleConnectionReader serverSessionPreambleReader)
        {
        }

        void OnDuplexConnection(IConnection connection, Action connectionDequeuedCallback,
            long streamPosition, int offset, int size, TimeSpan timeout)
        {
            if (onSessionPreambleKnown == null)
            {
                onSessionPreambleKnown = OnSessionPreambleKnown;
            }
            ServerSessionPreambleConnectionReader sessionPreambleReader = new ServerSessionPreambleConnectionReader(
                connection, connectionDequeuedCallback, streamPosition, offset, size,
                transportSettingsCallback, onConnectionClosed, onSessionPreambleKnown);
            lock (ThisLock)
            {
                if (isDisposed)
                {
                    sessionPreambleReader.Dispose();
                    return;
                }

                connectionReaders.Add(sessionPreambleReader);
            }

            sessionPreambleReader.StartReading(viaDelegate, timeout);
        }

        public void StartDemuxing()
        {
            StartDemuxing(null);
        }

        public void StartDemuxing(Action<Uri> viaDelegate)
        {
            this.viaDelegate = viaDelegate;
            acceptor.StartAccepting();
        }

        class ReuseConnectionState
        {
            ConnectionModeReader modeReader;
            TimeSpan closeTimeout;

            public ReuseConnectionState(ConnectionModeReader modeReader, TimeSpan closeTimeout)
            {
                this.modeReader = modeReader;
                this.closeTimeout = closeTimeout;
            }

            public ConnectionModeReader ModeReader
            {
                get { return modeReader; }
            }

            public TimeSpan CloseTimeout
            {
                get { return closeTimeout; }
            }
        }
    }

}
