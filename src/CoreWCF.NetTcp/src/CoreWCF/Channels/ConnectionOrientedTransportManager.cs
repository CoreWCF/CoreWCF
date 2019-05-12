using System;
using CoreWCF;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace CoreWCF.Channels
{
    abstract class ConnectionOrientedTransportManager<TChannelListener> : TransportManager
        where TChannelListener : ConnectionOrientedTransportChannelListener
    {
        UriPrefixTable<TChannelListener> addressTable;
        int connectionBufferSize;
        TimeSpan channelInitializationTimeout;
        int maxPendingConnections;
        TimeSpan maxOutputDelay;
        int maxPendingAccepts;
        TimeSpan idleTimeout;
        int maxPooledConnections;
        Action messageReceivedCallback;

        protected ConnectionOrientedTransportManager()
        {
            addressTable = new UriPrefixTable<TChannelListener>();
        }

        UriPrefixTable<TChannelListener> AddressTable
        {
            get { return addressTable; }
        }

        protected TimeSpan ChannelInitializationTimeout
        {
            get
            {
                return channelInitializationTimeout;
            }
        }

        internal void ApplyListenerSettings(IConnectionOrientedListenerSettings listenerSettings)
        {
            connectionBufferSize = listenerSettings.ConnectionBufferSize;
            channelInitializationTimeout = listenerSettings.ChannelInitializationTimeout;
            maxPendingConnections = listenerSettings.MaxPendingConnections;
            maxOutputDelay = listenerSettings.MaxOutputDelay;
            maxPendingAccepts = listenerSettings.MaxPendingAccepts;
            idleTimeout = listenerSettings.IdleTimeout;
            maxPooledConnections = listenerSettings.MaxPooledConnections;
        }

        internal int ConnectionBufferSize
        {
            get
            {
                return connectionBufferSize;
            }
        }

        internal int MaxPendingConnections
        {
            get
            {
                return maxPendingConnections;
            }
        }

        internal TimeSpan MaxOutputDelay
        {
            get
            {
                return maxOutputDelay;
            }
        }

        internal int MaxPendingAccepts
        {
            get
            {
                return maxPendingAccepts;
            }
        }

        internal TimeSpan IdleTimeout
        {
            get { return idleTimeout; }
        }

        internal int MaxPooledConnections
        {
            get { return maxPooledConnections; }
        }

        internal bool IsCompatible(ConnectionOrientedTransportChannelListener channelListener)
        {
            if (channelListener.InheritBaseAddressSettings)
                return true;

            return (
                (ChannelInitializationTimeout == channelListener.ChannelInitializationTimeout) &&
                (ConnectionBufferSize == channelListener.ConnectionBufferSize) &&
                (MaxPendingConnections == channelListener.MaxPendingConnections) &&
                (MaxOutputDelay == channelListener.MaxOutputDelay) &&
                (MaxPendingAccepts == channelListener.MaxPendingAccepts) &&
                (idleTimeout == channelListener.IdleTimeout) &&
                (maxPooledConnections == channelListener.MaxPooledConnections)
                );
        }

        TChannelListener GetChannelListener(Uri via)
        {
            TChannelListener channelListener = null;
            if (AddressTable.TryLookupUri(via, HostNameComparisonMode.StrongWildcard, out channelListener))
            {
                return channelListener;
            }

            if (AddressTable.TryLookupUri(via, HostNameComparisonMode.Exact, out channelListener))
            {
                return channelListener;
            }

            AddressTable.TryLookupUri(via, HostNameComparisonMode.WeakWildcard, out channelListener);
            return channelListener;
        }

        internal void OnDemuxerError(Exception exception)
        {
            lock (ThisLock)
            {
                Fault(AddressTable, exception);
            }
        }

        internal ISingletonChannelListener OnGetSingletonMessageHandler(ServerSingletonPreambleConnectionReader serverSingletonPreambleReader)
        {
            Uri via = serverSingletonPreambleReader.Via;
            TChannelListener channelListener = GetChannelListener(via);

            if (channelListener != null)
            {
                if (channelListener is IChannelListener<IReplyChannel>)
                {
                    channelListener.RaiseMessageReceived();
                    return (ISingletonChannelListener)channelListener;
                }
                else
                {
                    serverSingletonPreambleReader.SendFault(FramingEncodingString.UnsupportedModeFault);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ProtocolException(SR.Format(SR.FramingModeNotSupported, FramingMode.Singleton)));
                }
            }
            else
            {
                serverSingletonPreambleReader.SendFault(FramingEncodingString.EndpointNotFoundFault);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new EndpointNotFoundException(SR.Format(SR.EndpointNotFound, via)));
            }
        }

        internal void OnHandleServerSessionPreamble(ServerSessionPreambleConnectionReader serverSessionPreambleReader,
            ConnectionDemuxer connectionDemuxer)
        {
            Uri via = serverSessionPreambleReader.Via;
            TChannelListener channelListener = GetChannelListener(via);

            if (channelListener != null)
            {
                ISessionPreambleHandler sessionPreambleHandler = channelListener as ISessionPreambleHandler;

                if (sessionPreambleHandler != null && channelListener is IChannelListener<IDuplexSessionChannel>)
                {
                    sessionPreambleHandler.HandleServerSessionPreamble(serverSessionPreambleReader, connectionDemuxer);
                }
                else
                {
                    serverSessionPreambleReader.SendFault(FramingEncodingString.UnsupportedModeFault);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ProtocolException(SR.Format(SR.FramingModeNotSupported, FramingMode.Duplex)));
                }
            }
            else
            {
                serverSessionPreambleReader.SendFault(FramingEncodingString.EndpointNotFoundFault);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new EndpointNotFoundException(SR.Format(SR.DuplexSessionListenerNotFound, via.ToString())));
            }
        }

        internal IConnectionOrientedTransportFactorySettings OnGetTransportFactorySettings(Uri via)
        {
            return GetChannelListener(via);
        }

        internal override void Register(TransportChannelListener channelListener)
        {
            AddressTable.RegisterUri(channelListener.Uri, channelListener.HostNameComparisonModeInternal,
                (TChannelListener)channelListener);

            channelListener.SetMessageReceivedCallback(new Action(OnMessageReceived));
        }

        internal override void Unregister(TransportChannelListener channelListener)
        {
            EnsureRegistered(AddressTable, (TChannelListener)channelListener, channelListener.HostNameComparisonModeInternal);
            AddressTable.UnregisterUri(channelListener.Uri, channelListener.HostNameComparisonModeInternal);
            channelListener.SetMessageReceivedCallback(null);
        }

        internal void SetMessageReceivedCallback(Action messageReceivedCallback)
        {
            this.messageReceivedCallback = messageReceivedCallback;
        }

        void OnMessageReceived()
        {
            Action callback = messageReceivedCallback;
            if (callback != null)
            {
                callback();
            }
        }
    }

}
