using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using CoreWCF.Runtime;
using CoreWCF.Runtime.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Permissions;
using CoreWCF;
using CoreWCF.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;

namespace CoreWCF.Channels
{
    class SocketConnection : IConnection
    {
        static EventHandler<SocketAsyncEventArgs> onReceiveAsyncCompleted;
        static EventHandler<SocketAsyncEventArgs> onSocketSendCompleted;

        // common state
        Socket socket;
        TimeSpan sendTimeout;
        TimeSpan readFinTimeout;
        TimeSpan receiveTimeout;
        CloseState _closeState;
        bool isShutdown;
        bool noDelay = false;
        bool aborted;
        TraceEventType exceptionEventType;

        // close state
        TimeoutHelper closeTimeoutHelper;
        static Action<object> onWaitForFinComplete = new Action<object>(OnWaitForFinComplete);

        // read state
        int asyncReadSize;
        SocketAsyncEventArgs asyncReadEventArgs;
        byte[] readBuffer;
        int asyncReadBufferSize;
        object asyncReadState;
        Action<object> asyncReadCallback;
        Exception asyncReadException;
        bool asyncReadPending;

        // write state
        SocketAsyncEventArgs asyncWriteEventArgs;
        object asyncWriteState;
        Action<object> asyncWriteCallback;
        Exception asyncWriteException;
        bool asyncWritePending;

        IOThreadTimer receiveTimer;
        static Action<object> onReceiveTimeout;
        IOThreadTimer sendTimer;
        static Action<object> onSendTimeout;
        string timeoutErrorString;
        TransferOperation timeoutErrorTransferOperation;
        IPEndPoint remoteEndpoint;
        ConnectionBufferPool connectionBufferPool;
        string remoteEndpointAddress;

        public SocketConnection(Socket socket, ConnectionBufferPool connectionBufferPool, bool autoBindToCompletionPort)
        {
            Fx.Assert(autoBindToCompletionPort, "Not binding to completion port isn't supported in by WCF Core");

            if (socket == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("socket");
            }

            Fx.Assert(connectionBufferPool != null, "Argument connectionBufferPool cannot be null");

            CloseStateVal = CloseState.Open;
            exceptionEventType = TraceEventType.Error;
            this.socket = socket;
            this.connectionBufferPool = connectionBufferPool;
            readBuffer = this.connectionBufferPool.Take();
            asyncReadBufferSize = readBuffer.Length;
            this.socket.SendBufferSize = this.socket.ReceiveBufferSize = asyncReadBufferSize;
            sendTimeout = receiveTimeout = TimeSpan.MaxValue;

            remoteEndpoint = null;

            if (autoBindToCompletionPort)
            {
                this.socket.UseOnlyOverlappedIO = false;
            }
        }

        private CloseState CloseStateVal
        {
            get
            {
                return _closeState;
            }
            set
            {
                _closeState = value;
            }
        }
        public int AsyncReadBufferSize
        {
            get { return asyncReadBufferSize; }
        }

        public byte[] AsyncReadBuffer
        {
            get
            {
                return readBuffer;
            }
        }

        object ThisLock
        {
            get { return this; }
        }

        public TraceEventType ExceptionEventType
        {
            get { return exceptionEventType; }
            set { exceptionEventType = value; }
        }

        public IPEndPoint RemoteIPEndPoint
        {
            get
            {
                // this property should only be called on the receive path
                if (remoteEndpoint == null && CloseStateVal == CloseState.Open)
                {
                    try
                    {
                        remoteEndpoint = (IPEndPoint)socket.RemoteEndPoint;
                    }
                    catch (SocketException socketException)
                    {
                        // will never be a timeout error, so TimeSpan.Zero is ok
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                            ConvertReceiveException(socketException, TimeSpan.Zero), ExceptionEventType);
                    }
                    catch (ObjectDisposedException objectDisposedException)
                    {
                        Exception exceptionToThrow = ConvertObjectDisposedException(objectDisposedException, TransferOperation.Undefined);
                        if (object.ReferenceEquals(exceptionToThrow, objectDisposedException))
                        {
                            throw;
                        }
                        else
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelper(exceptionToThrow, ExceptionEventType);
                        }
                    }
                }

                return remoteEndpoint;
            }
        }

        IOThreadTimer SendTimer
        {
            get
            {
                if (sendTimer == null)
                {
                    if (onSendTimeout == null)
                    {
                        onSendTimeout = new Action<object>(OnSendTimeout);
                    }

                    sendTimer = new IOThreadTimer(onSendTimeout, this, false);
                }

                return sendTimer;
            }
        }

        IOThreadTimer ReceiveTimer
        {
            get
            {
                if (receiveTimer == null)
                {
                    if (onReceiveTimeout == null)
                    {
                        onReceiveTimeout = new Action<object>(OnReceiveTimeout);
                    }

                    receiveTimer = new IOThreadTimer(onReceiveTimeout, this, false);
                }

                return receiveTimer;
            }
        }


        string RemoteEndpointAddress
        {
            get
            {
                if (remoteEndpointAddress == null)
                {
                    try
                    {
                        IPEndPoint local, remote;
                        if (TryGetEndpoints(out local, out remote))
                        {
                            remoteEndpointAddress = GetRemoteEndpointAddressPort(remote);
                        }
                        else
                        {
                            //null indicates not initialized.
                            remoteEndpointAddress = string.Empty;
                        }
                    }
                    catch (Exception exception)
                    {
                        if (Fx.IsFatal(exception))
                        {
                            throw;
                        }

                    }
                }
                return remoteEndpointAddress;
            }
        }

        internal static string GetRemoteEndpointAddressPort(IPEndPoint iPEndPoint)
        {
            //We really don't want any exceptions
            if (iPEndPoint != null)
            {
                try
                {
                    return iPEndPoint.Address.ToString() + ":" + iPEndPoint.Port;
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                    {
                        throw;
                    }
                    //ignore and continue with all non-fatal exceptions.
                }
            }

            return string.Empty;
        }

        static void OnReceiveTimeout(object state)
        {
            SocketConnection thisPtr = (SocketConnection)state;
            thisPtr.Abort(SR.Format(SR.SocketAbortedReceiveTimedOut, thisPtr.receiveTimeout), TransferOperation.Read);
        }

        static void OnSendTimeout(object state)
        {
            SocketConnection thisPtr = (SocketConnection)state;
            thisPtr.Abort(TraceEventType.Warning,
                SR.Format(SR.SocketAbortedSendTimedOut, thisPtr.sendTimeout), TransferOperation.Write);
        }

        static void OnReceiveAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            ((SocketConnection)e.UserToken).OnReceiveAsync(sender, e);
        }

        static void OnSendAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            ((SocketConnection)e.UserToken).OnSendAsync(sender, e);
        }

        public void Abort()
        {
            Abort(null, TransferOperation.Undefined);
        }

        void Abort(string timeoutErrorString, TransferOperation transferOperation)
        {
            TraceEventType traceEventType = TraceEventType.Warning;

            // we could be timing out a cached connection
            if (ExceptionEventType == TraceEventType.Information)
            {
                traceEventType = ExceptionEventType;
            }

            Abort(traceEventType, timeoutErrorString, transferOperation);
        }

        void Abort(TraceEventType traceEventType)
        {
            Abort(traceEventType, null, TransferOperation.Undefined);
        }

        void Abort(TraceEventType traceEventType, string timeoutErrorString, TransferOperation transferOperation)
        {
            lock (ThisLock)
            {
                if (CloseStateVal == CloseState.Closed)
                {
                    return;
                }

                this.timeoutErrorString = timeoutErrorString;
                timeoutErrorTransferOperation = transferOperation;
                aborted = true;
                CloseStateVal = CloseState.Closed;

                if (asyncReadPending)
                {
                    CancelReceiveTimer();
                }
                else
                {
                    DisposeReadEventArgs();
                }

                if (asyncWritePending)
                {
                    CancelSendTimer();
                }
                else
                {
                    DisposeWriteEventArgs();
                }
            }

            socket.Close(0);
        }

        void AbortRead()
        {
            lock (ThisLock)
            {
                if (asyncReadPending)
                {
                    if (CloseStateVal != CloseState.Closed)
                    {
                        SetUserToken(asyncReadEventArgs, null);
                        asyncReadPending = false;
                        CancelReceiveTimer();
                    }
                    else
                    {
                        DisposeReadEventArgs();
                    }
                }
            }
        }

        void CancelReceiveTimer()
        {
            // CSDMain 34539: Snapshot the timer so that we don't null ref if there is a race
            // between calls to CancelReceiveTimer (e.g., Abort, AsyncReadCallback)

            IOThreadTimer receiveTimerSnapshot = receiveTimer;
            receiveTimer = null;

            if (receiveTimerSnapshot != null)
            {
                receiveTimerSnapshot.Cancel();
            }
        }

        void CancelSendTimer()
        {
            IOThreadTimer sendTimerSnapshot = sendTimer;
            sendTimer = null;

            if (sendTimerSnapshot != null)
            {
                sendTimerSnapshot.Cancel();
            }
        }

        void CloseAsyncAndLinger()
        {
            readFinTimeout = closeTimeoutHelper.RemainingTime();

            try
            {
                if (BeginReadCore(0, 1, readFinTimeout, onWaitForFinComplete, this) == AsyncCompletionResult.Queued)
                {
                    return;
                }

                int bytesRead = EndRead();

                if (bytesRead > 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                        new CommunicationException(SR.Format(SR.SocketCloseReadReceivedData, socket.RemoteEndPoint)),
                        ExceptionEventType);
                }
            }
            catch (TimeoutException timeoutException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(new TimeoutException(
                    SR.Format(SR.SocketCloseReadTimeout, socket.RemoteEndPoint, readFinTimeout), timeoutException),
                    ExceptionEventType);
            }

            ContinueClose(closeTimeoutHelper.RemainingTime());
        }

        static void OnWaitForFinComplete(object state)
        {
            SocketConnection thisPtr = (SocketConnection)state;

            try
            {
                int bytesRead;

                try
                {
                    bytesRead = thisPtr.EndRead();

                    if (bytesRead > 0)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                            new CommunicationException(SR.Format(SR.SocketCloseReadReceivedData, thisPtr.socket.RemoteEndPoint)),
                            thisPtr.ExceptionEventType);
                    }
                }
                catch (TimeoutException timeoutException)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(new TimeoutException(
                        SR.Format(SR.SocketCloseReadTimeout, thisPtr.socket.RemoteEndPoint, thisPtr.readFinTimeout),
                        timeoutException), thisPtr.ExceptionEventType);
                }

                thisPtr.ContinueClose(thisPtr.closeTimeoutHelper.RemainingTime());
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                DiagnosticUtility.TraceHandledException(e, TraceEventType.Warning);

                // The user has no opportunity to clean up the connection in the async and linger
                // code path, ensure cleanup finishes.
                thisPtr.Abort();
            }
        }

        public void Close(TimeSpan timeout, bool asyncAndLinger)
        {
            lock (ThisLock)
            {
                if (CloseStateVal == CloseState.Closing || CloseStateVal == CloseState.Closed)
                {
                    // already closing or closed, so just return
                    return;
                }
                CloseStateVal = CloseState.Closing;
            }

            // first we shutdown our send-side
            closeTimeoutHelper = new TimeoutHelper(timeout);
            Shutdown(closeTimeoutHelper.RemainingTime());

            if (asyncAndLinger)
            {
                CloseAsyncAndLinger();
            }
            else
            {
                CloseSync();
            }
        }

        void CloseSync()
        {
            byte[] dummy = new byte[1];

            // then we check for a FIN from the other side (i.e. read zero)
            int bytesRead;
            readFinTimeout = closeTimeoutHelper.RemainingTime();

            try
            {
                bytesRead = ReadCore(dummy, 0, 1, readFinTimeout, true);

                if (bytesRead > 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                        new CommunicationException(SR.Format(SR.SocketCloseReadReceivedData, socket.RemoteEndPoint)), ExceptionEventType);
                }
            }
            catch (TimeoutException timeoutException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(new TimeoutException(
                    SR.Format(SR.SocketCloseReadTimeout, socket.RemoteEndPoint, readFinTimeout), timeoutException), ExceptionEventType);
            }

            // finally we call Close with whatever time is remaining
            ContinueClose(closeTimeoutHelper.RemainingTime());
        }

        public void ContinueClose(TimeSpan timeout)
        {
            socket.Close(TimeoutHelper.ToMilliseconds(timeout));

            lock (ThisLock)
            {
                // Abort could have been called on a separate thread and cleaned up 
                // our buffers/completion here
                if (CloseStateVal != CloseState.Closed)
                {
                    if (!asyncReadPending)
                    {
                        DisposeReadEventArgs();
                    }

                    if (!asyncWritePending)
                    {
                        DisposeWriteEventArgs();
                    }
                }

                CloseStateVal = CloseState.Closed;
            }
        }

        public void Shutdown(TimeSpan timeout)
        {
            lock (ThisLock)
            {
                if (isShutdown)
                {
                    return;
                }

                isShutdown = true;
            }

            try
            {
                socket.Shutdown(SocketShutdown.Send);
            }
            catch (SocketException socketException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                    ConvertSendException(socketException, TimeSpan.MaxValue), ExceptionEventType);
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                Exception exceptionToThrow = ConvertObjectDisposedException(objectDisposedException, TransferOperation.Undefined);
                if (object.ReferenceEquals(exceptionToThrow, objectDisposedException))
                {
                    throw;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(exceptionToThrow, ExceptionEventType);
                }
            }
        }

        void ThrowIfNotOpen()
        {
            if (CloseStateVal == CloseState.Closing || CloseStateVal == CloseState.Closed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                    ConvertObjectDisposedException(new ObjectDisposedException(
                    GetType().ToString(), SR.Format(SR.SocketConnectionDisposed)), TransferOperation.Undefined), ExceptionEventType);
            }
        }

        void ThrowIfClosed()
        {
            if (CloseStateVal == CloseState.Closed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                    ConvertObjectDisposedException(new ObjectDisposedException(
                    GetType().ToString(), SR.Format(SR.SocketConnectionDisposed)), TransferOperation.Undefined), ExceptionEventType);
            }
        }

        bool TryGetEndpoints(out IPEndPoint localIPEndpoint, out IPEndPoint remoteIPEndpoint)
        {
            localIPEndpoint = null;
            remoteIPEndpoint = null;

            if (CloseStateVal == CloseState.Open)
            {
                try
                {
                    remoteIPEndpoint = remoteEndpoint ?? (IPEndPoint)socket.RemoteEndPoint;
                    localIPEndpoint = (IPEndPoint)socket.LocalEndPoint;
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                    {
                        throw;
                    }

                    DiagnosticUtility.TraceHandledException(exception, TraceEventType.Warning);
                }
            }

            return localIPEndpoint != null && remoteIPEndpoint != null;
        }

        public object DuplicateAndClose(int targetProcessId)
        {
            object result = socket.DuplicateAndClose(targetProcessId);
            Abort(TraceEventType.Information);
            return result;
        }

        public object GetCoreTransport()
        {
            return socket;
        }

        public Task<bool> ValidateAsync(Uri uri)
        {
            return Task.FromResult(true);
        }

        Exception ConvertSendException(SocketException socketException, TimeSpan remainingTime)
        {
            return ConvertTransferException(socketException, sendTimeout, socketException,
                TransferOperation.Write, aborted, timeoutErrorString, timeoutErrorTransferOperation, this, remainingTime);
        }

        Exception ConvertReceiveException(SocketException socketException, TimeSpan remainingTime)
        {
            return ConvertTransferException(socketException, receiveTimeout, socketException,
                TransferOperation.Read, aborted, timeoutErrorString, timeoutErrorTransferOperation, this, remainingTime);
        }

        internal static Exception ConvertTransferException(SocketException socketException, TimeSpan timeout, Exception originalException)
        {
            return ConvertTransferException(socketException, timeout, originalException,
                TransferOperation.Undefined, false, null, TransferOperation.Undefined, null, TimeSpan.MaxValue);
        }

        Exception ConvertObjectDisposedException(ObjectDisposedException originalException, TransferOperation transferOperation)
        {
            if (timeoutErrorString != null)
            {
                return ConvertTimeoutErrorException(originalException, transferOperation, timeoutErrorString, timeoutErrorTransferOperation);
            }
            else if (aborted)
            {
                return new CommunicationObjectAbortedException(SR.Format(SR.SocketConnectionDisposed), originalException);
            }
            else
            {
                return originalException;
            }
        }

        static Exception ConvertTransferException(SocketException socketException, TimeSpan timeout, Exception originalException,
            TransferOperation transferOperation, bool aborted, string timeoutErrorString, TransferOperation timeoutErrorTransferOperation,
            SocketConnection socketConnection, TimeSpan remainingTime)
        {
            if (socketException.ErrorCode == NativeSocketErrors.ERROR_INVALID_HANDLE)
            {
                return new CommunicationObjectAbortedException(socketException.Message, socketException);
            }

            if (timeoutErrorString != null)
            {
                return ConvertTimeoutErrorException(originalException, transferOperation, timeoutErrorString, timeoutErrorTransferOperation);
            }

            TraceEventType exceptionEventType = socketConnection == null ? TraceEventType.Error : socketConnection.ExceptionEventType;

            // 10053 can occur due to our timeout sockopt firing, so map to TimeoutException in that case
            if (socketException.ErrorCode == NativeSocketErrors.WSAECONNABORTED &&
                remainingTime <= TimeSpan.Zero)
            {
                TimeoutException timeoutException = new TimeoutException(SR.Format(SR.TcpConnectionTimedOut, timeout), originalException);
                return timeoutException;
            }

            if (socketException.ErrorCode == NativeSocketErrors.WSAENETRESET ||
                socketException.ErrorCode == NativeSocketErrors.WSAECONNABORTED ||
                socketException.ErrorCode == NativeSocketErrors.WSAECONNRESET)
            {
                if (aborted)
                {
                    return new CommunicationObjectAbortedException(SR.Format(SR.TcpLocalConnectionAborted), originalException);
                }
                else
                {
                    CommunicationException communicationException = new CommunicationException(SR.Format(SR.TcpConnectionResetError, timeout), originalException);
                    return communicationException;
                }
            }
            else if (socketException.ErrorCode == NativeSocketErrors.WSAETIMEDOUT)
            {
                TimeoutException timeoutException = new TimeoutException(SR.Format(SR.TcpConnectionTimedOut, timeout), originalException);
                return timeoutException;
            }
            else
            {
                if (aborted)
                {
                    return new CommunicationObjectAbortedException(SR.Format(SR.TcpTransferError, socketException.ErrorCode, socketException.Message), originalException);
                }
                else
                {
                    CommunicationException communicationException = new CommunicationException(SR.Format(SR.TcpTransferError, socketException.ErrorCode, socketException.Message), originalException);
                    return communicationException;
                }
            }
        }

        static Exception ConvertTimeoutErrorException(Exception originalException,
            TransferOperation transferOperation, string timeoutErrorString, TransferOperation timeoutErrorTransferOperation)
        {
            if (timeoutErrorString == null)
            {
                Fx.Assert("Argument timeoutErrorString must not be null.");
            }

            if (transferOperation == timeoutErrorTransferOperation)
            {
                return new TimeoutException(timeoutErrorString, originalException);
            }
            else
            {
                return new CommunicationException(timeoutErrorString, originalException);
            }
        }

        static string GetEndpointString(string sr, TimeSpan timeout, SocketException socketException, SocketConnection socketConnection)
        {
            IPEndPoint remoteEndpoint = null;
            IPEndPoint localEndpoint = null;
            bool haveEndpoints = socketConnection != null && socketConnection.TryGetEndpoints(out localEndpoint, out remoteEndpoint);

            if (string.Compare(sr, SR.TcpConnectionTimedOut, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return haveEndpoints
                    ? SR.Format(SR.TcpConnectionTimedOutWithIP, timeout, localEndpoint, remoteEndpoint)
                    : SR.Format(SR.TcpConnectionTimedOut, timeout);
            }
            else if (string.Compare(sr, SR.TcpConnectionResetError, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return haveEndpoints
                    ? SR.Format(SR.TcpConnectionResetErrorWithIP, timeout, localEndpoint, remoteEndpoint)
                    : SR.Format(SR.TcpConnectionResetError, timeout);
            }
            else
            {
                // sr == SR.TcpTransferError
                return haveEndpoints
                    ? SR.Format(SR.TcpTransferErrorWithIP, socketException.ErrorCode, socketException.Message, localEndpoint, remoteEndpoint)
                    : SR.Format(SR.TcpTransferError, socketException.ErrorCode, socketException.Message);
            }
        }

        public AsyncCompletionResult BeginWrite(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout,
            Action<object> callback, object state)
        {
            ConnectionUtilities.ValidateBufferBounds(buffer, offset, size);
            bool abortWrite = true;

            try
            {
                lock (ThisLock)
                {
                    Fx.Assert(!asyncWritePending, "Called BeginWrite twice.");
                    ThrowIfClosed();
                    EnsureWriteEventArgs();
                    SetImmediate(immediate);
                    SetWriteTimeout(timeout, false);
                    SetUserToken(asyncWriteEventArgs, this);
                    asyncWritePending = true;
                    asyncWriteCallback = callback;
                    asyncWriteState = state;
                }

                asyncWriteEventArgs.SetBuffer(buffer, offset, size);

                if (socket.SendAsync(asyncWriteEventArgs))
                {
                    abortWrite = false;
                    return AsyncCompletionResult.Queued;
                }

                HandleSendAsyncCompleted();
                abortWrite = false;
                return AsyncCompletionResult.Completed;
            }
            catch (SocketException socketException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                    ConvertSendException(socketException, TimeSpan.MaxValue), ExceptionEventType);
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                Exception exceptionToThrow = ConvertObjectDisposedException(objectDisposedException, TransferOperation.Write);
                if (object.ReferenceEquals(exceptionToThrow, objectDisposedException))
                {
                    throw;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(exceptionToThrow, ExceptionEventType);
                }
            }
            finally
            {
                if (abortWrite)
                {
                    AbortWrite();
                }
            }
        }

        public void EndWrite()
        {
            if (asyncWriteException != null)
            {
                AbortWrite();
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(asyncWriteException, ExceptionEventType);
            }

            lock (ThisLock)
            {
                if (!asyncWritePending)
                {
                    throw Fx.AssertAndThrow("SocketConnection.EndWrite called with no write pending.");
                }

                SetUserToken(asyncWriteEventArgs, null);
                asyncWritePending = false;

                if (CloseStateVal == CloseState.Closed)
                {
                    DisposeWriteEventArgs();
                }
            }
        }

        void OnSendAsync(object sender, SocketAsyncEventArgs eventArgs)
        {
            Fx.Assert(eventArgs != null, "Argument 'eventArgs' cannot be NULL.");
            CancelSendTimer();

            try
            {
                HandleSendAsyncCompleted();
                Fx.Assert(eventArgs.BytesTransferred == asyncWriteEventArgs.Count, "The socket SendAsync did not send all the bytes.");
            }
            catch (SocketException socketException)
            {
                asyncWriteException = ConvertSendException(socketException, TimeSpan.MaxValue);
            }
            catch (Exception exception)
            {
                if (Fx.IsFatal(exception))
                {
                    throw;
                }

                asyncWriteException = exception;
            }

            FinishWrite();
        }

        void HandleSendAsyncCompleted()
        {
            if (asyncWriteEventArgs.SocketError == SocketError.Success)
            {
                return;
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SocketException((int)asyncWriteEventArgs.SocketError));
        }

        // This method should be called inside ThisLock
        void DisposeWriteEventArgs()
        {
            if (asyncWriteEventArgs != null)
            {
                asyncWriteEventArgs.Completed -= onSocketSendCompleted;
                asyncWriteEventArgs.Dispose();
            }
        }

        void AbortWrite()
        {
            lock (ThisLock)
            {
                if (asyncWritePending)
                {
                    if (CloseStateVal != CloseState.Closed)
                    {
                        SetUserToken(asyncWriteEventArgs, null);
                        asyncWritePending = false;
                        CancelSendTimer();
                    }
                    else
                    {
                        DisposeWriteEventArgs();
                    }
                }
            }
        }

        void FinishWrite()
        {
            Action<object> asyncWriteCallback = this.asyncWriteCallback;
            object asyncWriteState = this.asyncWriteState;

            this.asyncWriteState = null;
            this.asyncWriteCallback = null;

            asyncWriteCallback(asyncWriteState);
        }

        public void Write(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout)
        {
            // as per http://support.microsoft.com/default.aspx?scid=kb%3ben-us%3b201213
            // we shouldn't write more than 64K synchronously to a socket
            const int maxSocketWrite = 64 * 1024;

            ConnectionUtilities.ValidateBufferBounds(buffer, offset, size);

            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            try
            {
                SetImmediate(immediate);
                int bytesToWrite = size;

                while (bytesToWrite > 0)
                {
                    SetWriteTimeout(timeoutHelper.RemainingTime(), true);
                    size = Math.Min(bytesToWrite, maxSocketWrite);
                    socket.Send(buffer, offset, size, SocketFlags.None);
                    bytesToWrite -= size;
                    offset += size;
                    timeout = timeoutHelper.RemainingTime();
                }
            }
            catch (SocketException socketException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                    ConvertSendException(socketException, timeoutHelper.RemainingTime()), ExceptionEventType);
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                Exception exceptionToThrow = ConvertObjectDisposedException(objectDisposedException, TransferOperation.Write);
                if (object.ReferenceEquals(exceptionToThrow, objectDisposedException))
                {
                    throw;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(exceptionToThrow, ExceptionEventType);
                }
            }
        }

        public void Write(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout, BufferManager bufferManager)
        {
            try
            {
                Write(buffer, offset, size, immediate, timeout);
            }
            finally
            {
                bufferManager.ReturnBuffer(buffer);
            }
        }

        public int Read(byte[] buffer, int offset, int size, TimeSpan timeout)
        {
            ConnectionUtilities.ValidateBufferBounds(buffer, offset, size);
            ThrowIfNotOpen();
            return ReadCore(buffer, offset, size, timeout, false);
        }

        int ReadCore(byte[] buffer, int offset, int size, TimeSpan timeout, bool closing)
        {
            int bytesRead = 0;
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            try
            {
                SetReadTimeout(timeoutHelper.RemainingTime(), true, closing);
                bytesRead = socket.Receive(buffer, offset, size, SocketFlags.None);
            }
            catch (SocketException socketException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                    ConvertReceiveException(socketException, timeoutHelper.RemainingTime()), ExceptionEventType);
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                Exception exceptionToThrow = ConvertObjectDisposedException(objectDisposedException, TransferOperation.Read);
                if (object.ReferenceEquals(exceptionToThrow, objectDisposedException))
                {
                    throw;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(exceptionToThrow, ExceptionEventType);
                }
            }

            return bytesRead;
        }

        public virtual AsyncCompletionResult BeginRead(int offset, int size, TimeSpan timeout,
            Action<object> callback, object state)
        {
            ConnectionUtilities.ValidateBufferBounds(AsyncReadBufferSize, offset, size);
            ThrowIfNotOpen();
            return BeginReadCore(offset, size, timeout, callback, state);
        }

        AsyncCompletionResult BeginReadCore(int offset, int size, TimeSpan timeout,
            Action<object> callback, object state)
        {
            bool abortRead = true;

            lock (ThisLock)
            {
                ThrowIfClosed();
                EnsureReadEventArgs();
                asyncReadState = state;
                asyncReadCallback = callback;
                SetUserToken(asyncReadEventArgs, this);
                asyncReadPending = true;
                SetReadTimeout(timeout, false, false);
            }

            try
            {
                if (offset != asyncReadEventArgs.Offset ||
                    size != asyncReadEventArgs.Count)
                {
                    asyncReadEventArgs.SetBuffer(offset, size);
                }

                if (ReceiveAsync())
                {
                    abortRead = false;
                    return AsyncCompletionResult.Queued;
                }

                HandleReceiveAsyncCompleted();
                asyncReadSize = asyncReadEventArgs.BytesTransferred;

                abortRead = false;
                return AsyncCompletionResult.Completed;
            }
            catch (SocketException socketException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(ConvertReceiveException(socketException, TimeSpan.MaxValue), ExceptionEventType);
            }
            catch (ObjectDisposedException objectDisposedException)
            {
                Exception exceptionToThrow = ConvertObjectDisposedException(objectDisposedException, TransferOperation.Read);
                if (object.ReferenceEquals(exceptionToThrow, objectDisposedException))
                {
                    throw;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(exceptionToThrow, ExceptionEventType);
                }
            }
            finally
            {
                if (abortRead)
                {
                    AbortRead();
                }
            }
        }

        bool ReceiveAsync()
        {
            return socket.ReceiveAsync(asyncReadEventArgs);
        }

        void OnReceiveAsync(object sender, SocketAsyncEventArgs eventArgs)
        {
            Fx.Assert(eventArgs != null, "Argument 'eventArgs' cannot be NULL.");
            CancelReceiveTimer();

            try
            {
                HandleReceiveAsyncCompleted();
                asyncReadSize = eventArgs.BytesTransferred;
            }
            catch (SocketException socketException)
            {
                asyncReadException = ConvertReceiveException(socketException, TimeSpan.MaxValue);
            }
            catch (Exception exception)
            {
                asyncReadException = exception;
                if (Fx.IsFatal(exception))
                {
                    throw;
                }
            }

            FinishRead();
        }

        void HandleReceiveAsyncCompleted()
        {
            if (asyncReadEventArgs.SocketError == SocketError.Success)
            {
                return;
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SocketException((int)asyncReadEventArgs.SocketError));
        }

        void FinishRead()
        {
            Action<object> asyncReadCallback = this.asyncReadCallback;
            object asyncReadState = this.asyncReadState;

            this.asyncReadState = null;
            this.asyncReadCallback = null;

            asyncReadCallback(asyncReadState);
        }

        // Both BeginRead/ReadAsync paths completed themselves. EndRead's only job is to deliver the result.
        public int EndRead()
        {
            if (asyncReadException != null)
            {
                AbortRead();
                throw DiagnosticUtility.ExceptionUtility.ThrowHelper(asyncReadException, ExceptionEventType);
            }

            lock (ThisLock)
            {
                if (!asyncReadPending)
                {
                    throw Fx.AssertAndThrow("SocketConnection.EndRead called with no read pending.");
                }

                SetUserToken(asyncReadEventArgs, null);
                asyncReadPending = false;

                if (CloseStateVal == CloseState.Closed)
                {
                    DisposeReadEventArgs();
                }
            }

            return asyncReadSize;
        }

        // This method should be called inside ThisLock
        void DisposeReadEventArgs()
        {
            Fx.Assert(Monitor.IsEntered(ThisLock), "Lock must be taken");
            if (asyncReadEventArgs != null)
            {
                asyncReadEventArgs.Completed -= onReceiveAsyncCompleted;
                asyncReadEventArgs.Dispose();
            }

            // We release the buffer only if there is no outstanding I/O
            TryReturnReadBuffer();
        }

        void TryReturnReadBuffer()
        {
            // The buffer must not be returned and nulled when an abort occurs. Since the buffer
            // is also accessed by higher layers, code that has not yet realized the stack is
            // aborted may be attempting to read from the buffer.
            if (readBuffer != null && !aborted)
            {
                connectionBufferPool.Return(readBuffer);
                readBuffer = null;
            }
        }

        void SetUserToken(SocketAsyncEventArgs args, object userToken)
        {
            // The socket args can be pinned by the overlapped callback. Ensure SocketConnection is
            // only pinned when there is outstanding IO.
            if (args != null)
            {
                args.UserToken = userToken;
            }
        }

        void SetImmediate(bool immediate)
        {
            if (immediate != noDelay)
            {
                lock (ThisLock)
                {
                    ThrowIfNotOpen();
                    socket.NoDelay = immediate;
                }
                noDelay = immediate;
            }
        }

        void SetReadTimeout(TimeSpan timeout, bool synchronous, bool closing)
        {
            if (synchronous)
            {
                CancelReceiveTimer();

                // 0 == infinite for winsock timeouts, so we should preempt and throw
                if (timeout <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                        new TimeoutException(SR.Format(SR.TcpConnectionTimedOut, timeout)), ExceptionEventType);
                }

                if (UpdateTimeout(receiveTimeout, timeout))
                {
                    lock (ThisLock)
                    {
                        if (!closing || CloseStateVal != CloseState.Closing)
                        {
                            ThrowIfNotOpen();
                        }
                        socket.ReceiveTimeout = TimeoutHelper.ToMilliseconds(timeout);
                    }
                    receiveTimeout = timeout;
                }
            }
            else
            {
                receiveTimeout = timeout;
                if (timeout == TimeSpan.MaxValue)
                {
                    CancelReceiveTimer();
                }
                else
                {
                    ReceiveTimer.Set(timeout);
                }
            }
        }

        void SetWriteTimeout(TimeSpan timeout, bool synchronous)
        {
            if (synchronous)
            {
                CancelSendTimer();

                // 0 == infinite for winsock timeouts, so we should preempt and throw
                if (timeout <= TimeSpan.Zero)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                        new TimeoutException(SR.Format(SR.TcpConnectionTimedOut, timeout)), ExceptionEventType);
                }

                if (UpdateTimeout(sendTimeout, timeout))
                {
                    lock (ThisLock)
                    {
                        ThrowIfNotOpen();
                        socket.SendTimeout = TimeoutHelper.ToMilliseconds(timeout);
                    }
                    sendTimeout = timeout;
                }
            }
            else
            {
                sendTimeout = timeout;
                if (timeout == TimeSpan.MaxValue)
                {
                    CancelSendTimer();
                }
                else
                {
                    SendTimer.Set(timeout);
                }
            }
        }

        bool UpdateTimeout(TimeSpan oldTimeout, TimeSpan newTimeout)
        {
            if (oldTimeout == newTimeout)
            {
                return false;
            }

            long threshold = oldTimeout.Ticks / 10;
            long delta = Math.Max(oldTimeout.Ticks, newTimeout.Ticks) - Math.Min(oldTimeout.Ticks, newTimeout.Ticks);

            return delta > threshold;
        }

        // This method should be called inside ThisLock
        void EnsureReadEventArgs()
        {
            if (asyncReadEventArgs == null)
            {
                // Init ReadAsync state
                if (onReceiveAsyncCompleted == null)
                {
                    onReceiveAsyncCompleted = new EventHandler<SocketAsyncEventArgs>(OnReceiveAsyncCompleted);
                }

                asyncReadEventArgs = new SocketAsyncEventArgs();
                asyncReadEventArgs.SetBuffer(readBuffer, 0, readBuffer.Length);
                asyncReadEventArgs.Completed += onReceiveAsyncCompleted;
            }
        }

        // This method should be called inside ThisLock
        void EnsureWriteEventArgs()
        {
            if (asyncWriteEventArgs == null)
            {
                // Init SendAsync state
                if (onSocketSendCompleted == null)
                {
                    onSocketSendCompleted = new EventHandler<SocketAsyncEventArgs>(OnSendAsyncCompleted);
                }

                asyncWriteEventArgs = new SocketAsyncEventArgs();
                asyncWriteEventArgs.Completed += onSocketSendCompleted;
            }
        }

        enum CloseState
        {
            Open,
            Closing,
            Closed,
        }

        enum TransferOperation
        {
            Write,
            Read,
            Undefined,
        }
    }

    internal interface ISocketListenerSettings
    {
        int BufferSize { get; }
        int ListenBacklog { get; }
    }

    class SocketConnectionListener : IConnectionListener
    {
        IPEndPoint localEndpoint;
        bool isDisposed;
        bool isListening;
        Socket listenSocket;
        ISocketListenerSettings settings;
        bool useOnlyOverlappedIO;
        ConnectionBufferPool connectionBufferPool;
        SocketAsyncEventArgsPool socketAsyncEventArgsPool;
        AsyncLock asyncLock = new AsyncLock();

        static EventHandler<SocketAsyncEventArgs> s_acceptAsyncCompleted = new EventHandler<SocketAsyncEventArgs>(AcceptAsyncCompleted);

        public SocketConnectionListener(Socket listenSocket, ISocketListenerSettings settings, bool useOnlyOverlappedIO)
            : this(settings, useOnlyOverlappedIO)
        {
            this.listenSocket = listenSocket;
        }

        public SocketConnectionListener(IPEndPoint localEndpoint, ISocketListenerSettings settings, bool useOnlyOverlappedIO)
            : this(settings, useOnlyOverlappedIO)
        {
            this.localEndpoint = localEndpoint;
        }

        SocketConnectionListener(ISocketListenerSettings settings, bool useOnlyOverlappedIO)
        {
            Fx.Assert(settings != null, "Input settings should not be null");
            this.settings = settings;
            this.useOnlyOverlappedIO = useOnlyOverlappedIO;
            connectionBufferPool = new ConnectionBufferPool(settings.BufferSize);
        }

        AsyncLock ThisLock
        {
            get { return asyncLock; }
        }

        public async Task<IConnection> AcceptAsync()
        {
            var socketAsyncEventArgs = TakeSocketAsyncEventArgs();
            socketAsyncEventArgs.Completed += s_acceptAsyncCompleted;
            try
            {
                while (true)
                {
                    try
                    {
                        var socket = await InternalAcceptAsync(socketAsyncEventArgs);
                        if (socket == null)
                            return null;

                        return new SocketConnection(socket, connectionBufferPool, true);
                    }
                    catch (SocketException socketException)
                    {
                        if (ShouldAcceptRecover(socketException))
                        {
                            continue;
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
            finally
            {
                Fx.Assert(socketAsyncEventArgs.UserToken == null, "UserToken should be nulled out by the same method that sets it");
                socketAsyncEventArgs.Completed -= s_acceptAsyncCompleted;
                ReturnSocketAsyncEventArgs(socketAsyncEventArgs);
            }
        }

        internal async Task<Socket> InternalAcceptAsync(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            using (await ThisLock.TakeLockAsync())
            {
                if (isDisposed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().ToString(), SR.SocketListenerDisposed));
                }

                if (!isListening)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SocketListenerNotListening));
                }

                return await DoAcceptAsync(socketAsyncEventArgs);
            }
        }

        private async Task<Socket> DoAcceptAsync(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            SocketAsyncEventArgsPool.CleanupAcceptSocket(socketAsyncEventArgs);
            
            var tcs = new TaskCompletionSource<Socket>(this);
            socketAsyncEventArgs.UserToken = tcs;

            if (!listenSocket.AcceptAsync(socketAsyncEventArgs))
            {
                HandleAcceptAsyncCompleted(socketAsyncEventArgs);
            }

            Socket result;
            try
            {
                result = await tcs.Task;
            }
            catch(Exception e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(e);
            }
            finally
            {
                socketAsyncEventArgs.UserToken = null;
            }

            return result;
        }

        SocketException HandleAcceptAsyncCompleted(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            SocketException completionException = null;
            var tcs = (TaskCompletionSource<Socket>)socketAsyncEventArgs.UserToken;
            if (socketAsyncEventArgs.SocketError == SocketError.Success)
            {
                var socket = socketAsyncEventArgs.AcceptSocket;
                socketAsyncEventArgs.AcceptSocket = null;
                tcs.SetResult(socket);
            }
            else
            {
                completionException = new SocketException((int)socketAsyncEventArgs.SocketError);
                tcs.TrySetException(completionException);
            }

            return completionException;
        }

        private static void AcceptAsyncCompleted(object sender, SocketAsyncEventArgs e)
        {
            var tcs = (TaskCompletionSource<Socket>)e.UserToken;
            var thisPtr = (SocketConnectionListener)tcs.Task.AsyncState;
            thisPtr.HandleAcceptAsyncCompleted(e);
        }

        static bool ShouldAcceptRecover(SocketException exception)
        {
            return (
                (exception.ErrorCode == NativeSocketErrors.WSAECONNRESET) ||
                (exception.ErrorCode == NativeSocketErrors.WSAEMFILE) ||
                (exception.ErrorCode == NativeSocketErrors.WSAENOBUFS) ||
                (exception.ErrorCode == NativeSocketErrors.WSAETIMEDOUT)
            );
        }

        SocketAsyncEventArgs TakeSocketAsyncEventArgs()
        {
            return socketAsyncEventArgsPool.Take();
        }

        void ReturnSocketAsyncEventArgs(SocketAsyncEventArgs socketAsyncEventArgs)
        {
            Fx.Assert(socketAsyncEventArgsPool != null, "The socketAsyncEventArgsPool should not be null");
            socketAsyncEventArgsPool.Return(socketAsyncEventArgs);
        }

        // This is the buffer size that is used by the System.Net for accepting new connections
        static int GetAcceptBufferSize(Socket listenSocket)
        {
            return (listenSocket.LocalEndPoint.Serialize().Size + 16) * 2;
        }

        public void Dispose()
        {
            using (ThisLock.TakeLock())
            {
                if (!isDisposed)
                {
                    if (listenSocket != null)
                    {
                        listenSocket.Close();
                    }

                    if (socketAsyncEventArgsPool != null)
                    {
                        socketAsyncEventArgsPool.Close();
                    }

                    isDisposed = true;
                }
            }
        }


        public void Listen()
        {
            // If you call listen() on a port, then kill the process, then immediately start a new process and 
            // try to listen() on the same port, you sometimes get WSAEADDRINUSE.  Even if nothing was accepted.  
            // Ports don't immediately free themselves on process shutdown.  We call listen() in a loop on a delay 
            // for a few iterations for this reason. 
            //
            TimeSpan listenTimeout = TimeSpan.FromSeconds(1);
            BackoffTimeoutHelper backoffHelper = new BackoffTimeoutHelper(listenTimeout);

            lock (ThisLock)
            {
                if (listenSocket != null)
                {
                    listenSocket.Listen(settings.ListenBacklog);
                    isListening = true;
                }

                while (!isListening)
                {
                    try
                    {
                        listenSocket = new Socket(localEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        listenSocket.Bind(localEndpoint);
                        listenSocket.Listen(settings.ListenBacklog);
                        isListening = true;
                    }
                    catch (SocketException socketException)
                    {
                        bool retry = false;

                        if (socketException.ErrorCode == NativeSocketErrors.WSAEADDRINUSE)
                        {
                            if (!backoffHelper.IsExpired())
                            {
                                backoffHelper.WaitAndBackoff();
                                retry = true;
                            }
                        }

                        if (!retry)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                SocketConnectionListener.ConvertListenException(socketException, localEndpoint));
                        }
                    }
                }

                socketAsyncEventArgsPool = new SocketAsyncEventArgsPool(GetAcceptBufferSize(listenSocket));
            }
        }

        public static Exception ConvertListenException(SocketException socketException, IPEndPoint localEndpoint)
        {
            if (socketException.ErrorCode == NativeSocketErrors.ERROR_INVALID_HANDLE)
            {
                return new CommunicationObjectAbortedException(socketException.Message, socketException);
            }
            if (socketException.ErrorCode == NativeSocketErrors.WSAEADDRINUSE)
            {
                return new AddressAlreadyInUseException(SR.Format(SR.TcpAddressInUse, localEndpoint.ToString()), socketException);
            }
            else
            {
                return new CommunicationException(
                    SR.Format(SR.TcpListenError, socketException.ErrorCode, socketException.Message, localEndpoint.ToString()),
                    socketException);
            }
        }
    }

    internal static class NativeSocketErrors
    {
        public const int ERROR_INVALID_HANDLE = 6;

        //public const int WSAACCESS = 10013;
        public const int WSAEMFILE = 10024;
        //public const int WSAEMSGSIZE = 10040;
        public const int WSAEADDRINUSE = 10048;
        //public const int WSAEADDRNOTAVAIL = 10049;
        //public const int WSAENETDOWN = 10050;
        //public const int WSAENETUNREACH = 10051;
        public const int WSAENETRESET = 10052;
        public const int WSAECONNABORTED = 10053;
        public const int WSAECONNRESET = 10054;
        public const int WSAENOBUFS = 10055;
        //public const int WSAESHUTDOWN = 10058;
        public const int WSAETIMEDOUT = 10060;
        //public const int WSAECONNREFUSED = 10061;
        //public const int WSAEHOSTDOWN = 10064;
        //public const int WSAEHOSTUNREACH = 10065;
    }
}
