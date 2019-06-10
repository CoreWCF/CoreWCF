using System;
using System.Diagnostics;
using System.IO;
using CoreWCF.Runtime;
using CoreWCF.Runtime.Diagnostics;
using CoreWCF;
using CoreWCF.Diagnostics;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    delegate IConnectionOrientedTransportFactorySettings TransportSettingsCallback(Uri via);
    delegate void ConnectionClosedCallback(InitialServerConnectionReader connectionReader);

    // Host for a connection that deals with structured close/abort and notifying the owner appropriately
    // used for cases where no one else (channel, etc) actually owns the reader
    abstract class InitialServerConnectionReader : IDisposable
    {
        int maxViaSize;
        int maxContentTypeSize;
        IConnection connection;
        Action connectionDequeuedCallback;
        ConnectionClosedCallback closedCallback;
        bool isClosed;

        protected InitialServerConnectionReader(IConnection connection, ConnectionClosedCallback closedCallback)
            : this(connection, closedCallback,
            ConnectionOrientedTransportDefaults.MaxViaSize, ConnectionOrientedTransportDefaults.MaxContentTypeSize)
        {
        }

        protected InitialServerConnectionReader(IConnection connection, ConnectionClosedCallback closedCallback, int maxViaSize, int maxContentTypeSize)
        {
            if (connection == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(connection));
            }

            if (closedCallback == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(closedCallback));
            }

            this.connection = connection;
            this.closedCallback = closedCallback;
            this.maxContentTypeSize = maxContentTypeSize;
            this.maxViaSize = maxViaSize;
        }

        public IConnection Connection
        {
            get { return connection; }
        }

        public Action ConnectionDequeuedCallback
        {
            get
            {
                return connectionDequeuedCallback;
            }

            set
            {
                connectionDequeuedCallback = value;
            }
        }

        public Action GetConnectionDequeuedCallback()
        {
            Action dequeuedCallback = connectionDequeuedCallback;
            connectionDequeuedCallback = null;
            return dequeuedCallback;
        }

        protected bool IsClosed
        {
            get { return isClosed; }
        }

        protected int MaxContentTypeSize
        {
            get
            {
                return maxContentTypeSize;
            }
        }

        protected int MaxViaSize
        {
            get
            {
                return maxViaSize;
            }
        }

        object ThisLock
        {
            get { return this; }
        }

        // used by the listener to release the connection object so it can be closed at a later time
        public void ReleaseConnection()
        {
            isClosed = true;
            connection = null;
        }

        // for cached connections -- try to shut down gracefully if possible
        public void CloseFromPool(TimeSpan timeout)
        {
            try
            {
                Close(timeout);
            }
            catch (CommunicationException communicationException)
            {
                DiagnosticUtility.TraceHandledException(communicationException, TraceEventType.Information);
            }
            catch (TimeoutException timeoutException)
            {
                DiagnosticUtility.TraceHandledException(timeoutException, TraceEventType.Information);
            }
        }

        public void Dispose()
        {
            lock (ThisLock)
            {
                if (isClosed)
                {
                    return;
                }

                isClosed = true;
            }

            IConnection connection = this.connection;
            if (connection != null)
            {
                connection.Abort();
            }

            if (connectionDequeuedCallback != null)
            {
                connectionDequeuedCallback();
            }
        }

        protected void Abort()
        {
            Abort(null);
        }

        internal void Abort(Exception e)
        {
            lock (ThisLock)
            {
                if (isClosed)
                    return;

                isClosed = true;
            }

            try
            {
                connection.Abort();
            }
            finally
            {
                if (closedCallback != null)
                {
                    closedCallback(this);
                }

                if (connectionDequeuedCallback != null)
                {
                    connectionDequeuedCallback();
                }
            }
        }

        protected void Close(TimeSpan timeout)
        {
            lock (ThisLock)
            {
                if (isClosed)
                    return;

                isClosed = true;
            }

            bool success = false;
            try
            {
                connection.Close(timeout, true);
                success = true;
            }
            finally
            {
                if (!success)
                {
                    connection.Abort();
                }

                if (closedCallback != null)
                {
                    closedCallback(this);
                }

                if (connectionDequeuedCallback != null)
                {
                    connectionDequeuedCallback();
                }
            }
        }

        internal static void SendFault(IConnection connection, string faultString, byte[] drainBuffer, TimeSpan sendTimeout, int maxRead)
        {
            EncodedFault encodedFault = new EncodedFault(faultString);
            TimeoutHelper timeoutHelper = new TimeoutHelper(sendTimeout);
            try
            {
                connection.Write(encodedFault.EncodedBytes, 0, encodedFault.EncodedBytes.Length, true, timeoutHelper.RemainingTime());
                connection.Shutdown(timeoutHelper.RemainingTime());
            }
            catch (CommunicationException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                connection.Abort();
                return;
            }
            catch (TimeoutException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                connection.Abort();
                return;
            }

            // make sure we read until EOF or a quota is hit
            int read = 0;
            int readTotal = 0;
            for (;;)
            {
                try
                {
                    read = connection.Read(drainBuffer, 0, drainBuffer.Length, timeoutHelper.RemainingTime());
                }
                catch (CommunicationException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    connection.Abort();
                    return;
                }
                catch (TimeoutException e)
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                    connection.Abort();
                    return;
                }

                if (read == 0)
                    break;

                readTotal += read;
                if (readTotal > maxRead || timeoutHelper.RemainingTime() <= TimeSpan.Zero)
                {
                    connection.Abort();
                    return;
                }
            }

            ConnectionUtilities.CloseNoThrow(connection, timeoutHelper.RemainingTime());
        }

        public static async Task<IConnection> UpgradeConnectionAsync(IConnection connection, StreamUpgradeAcceptor upgradeAcceptor, IDefaultCommunicationTimeouts defaultTimeouts)
        {
            ConnectionStream connectionStream = new ConnectionStream(connection, defaultTimeouts);
            Stream stream = await upgradeAcceptor.AcceptUpgradeAsync(connectionStream);
            return new StreamConnection(stream, connectionStream);
        }
    }
}
