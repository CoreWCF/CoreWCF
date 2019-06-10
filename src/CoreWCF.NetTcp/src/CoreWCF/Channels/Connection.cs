using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using CoreWCF.Runtime;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.Contracts;

namespace CoreWCF.Channels
{
    // Low level abstraction for a socket/pipe
    interface IConnection
    {
        byte[] AsyncReadBuffer { get; }
        int AsyncReadBufferSize { get; }
        TraceEventType ExceptionEventType { get; set; }
        IPEndPoint RemoteIPEndPoint { get; }

        void Abort();
        // TODO: Consider changing Close to CloseAsync
        void Close(TimeSpan timeout, bool asyncAndLinger);
        void Shutdown(TimeSpan timeout);

        // TODO: Modify IConnection.BeginWrite to take a CancellationToken instead of using it's own timer
        AsyncCompletionResult BeginWrite(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout, Action<object> callback, object state);
        void EndWrite();
        void Write(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout);
        void Write(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout, BufferManager bufferManager);

        int Read(byte[] buffer, int offset, int size, TimeSpan timeout);

        // TODO: Modify IConnection.BeginRead to take a CancellationToken instead of using it's own timer
        AsyncCompletionResult BeginRead(int offset, int size, TimeSpan timeout, Action<object> callback, object state);
        int EndRead();

        // very ugly listener stuff
        object DuplicateAndClose(int targetProcessId);
        object GetCoreTransport();
        Task<bool> ValidateAsync(Uri uri);
    }

    internal static class ConnectionExtentions
    {
        public static Task<int> ReadAsync(this IConnection connection, int offset, int size, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<int>(connection);
            if(connection.BeginRead(offset, size, timeout, HandleReadComplete, tcs)==AsyncCompletionResult.Completed)
            {
                HandleReadComplete(tcs);
            }

            return tcs.Task;
        }

        private static void HandleReadComplete(object state)
        {
            var tcs = (TaskCompletionSource<int>)state;
            var connection = (IConnection)tcs.Task.AsyncState;
            try
            {
                tcs.TrySetResult(connection.EndRead());
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        }

        public static Task WriteAsync(this IConnection connection, byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<bool>(connection);
            if (connection.BeginWrite(buffer, offset, size, immediate, timeout, HandleWriteComplete, tcs)==AsyncCompletionResult.Completed)
            {
                HandleWriteComplete(tcs);
            }

            return tcs.Task;
        }

        public static async Task WriteAsync(this IConnection connection, byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout, BufferManager bufferManager)
        {
            try
            {
                await WriteAsync(connection, buffer, offset, size, immediate, timeout);
            }
            finally
            {
                bufferManager.ReturnBuffer(buffer);
            }
        }

        private static void HandleWriteComplete(object state)
        {
            var tcs = (TaskCompletionSource<bool>)state;
            var connection = (IConnection)tcs.Task.AsyncState;
            try
            {
                connection.EndWrite();
                tcs.TrySetResult(true);
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        }
    }

    // Low level abstraction for listening for sockets/pipes
    interface IConnectionListener : IDisposable
    {
        void Listen();
        Task<IConnection> AcceptAsync();
    }

    abstract class DelegatingConnection : IConnection
    {
        IConnection connection;

        protected DelegatingConnection(IConnection connection)
        {
            this.connection = connection;
        }

        public virtual byte[] AsyncReadBuffer
        {
            get { return connection.AsyncReadBuffer; }
        }

        public virtual int AsyncReadBufferSize
        {
            get { return connection.AsyncReadBufferSize; }
        }

        public TraceEventType ExceptionEventType
        {
            get { return connection.ExceptionEventType; }
            set { connection.ExceptionEventType = value; }
        }

        protected IConnection Connection
        {
            get { return connection; }
        }

        public IPEndPoint RemoteIPEndPoint
        {
            get { return connection.RemoteIPEndPoint; }
        }

        public virtual void Abort()
        {
            connection.Abort();
        }

        public virtual void Close(TimeSpan timeout, bool asyncAndLinger)
        {
            connection.Close(timeout, asyncAndLinger);
        }

        public virtual void Shutdown(TimeSpan timeout)
        {
            connection.Shutdown(timeout);
        }

        public virtual object DuplicateAndClose(int targetProcessId)
        {
            return connection.DuplicateAndClose(targetProcessId);
        }

        public virtual object GetCoreTransport()
        {
            return connection.GetCoreTransport();
        }

        public virtual Task<bool> ValidateAsync(Uri uri)
        {
            return connection.ValidateAsync(uri);
        }

        public virtual AsyncCompletionResult BeginWrite(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout,
            Action<object> callback, object state)
        {
            return connection.BeginWrite(buffer, offset, size, immediate, timeout, callback, state);
        }

        public virtual void EndWrite()
        {
            connection.EndWrite();
        }

        public virtual void Write(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout)
        {
            connection.Write(buffer, offset, size, immediate, timeout);
        }

        public virtual void Write(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout, BufferManager bufferManager)
        {
            connection.Write(buffer, offset, size, immediate, timeout, bufferManager);
        }

        public virtual int Read(byte[] buffer, int offset, int size, TimeSpan timeout)
        {
            return connection.Read(buffer, offset, size, timeout);
        }

        public virtual AsyncCompletionResult BeginRead(int offset, int size, TimeSpan timeout,
            Action<object> callback, object state)
        {
            return connection.BeginRead(offset, size, timeout, callback, state);
        }

        public virtual int EndRead()
        {
            return connection.EndRead();
        }
    }

    class PreReadConnection : DelegatingConnection
    {
        int asyncBytesRead;
        byte[] preReadData;
        int preReadOffset;
        int preReadCount;

        public PreReadConnection(IConnection innerConnection, byte[] initialData, int initialOffset, int initialSize)
            : base(innerConnection)
        {
            preReadData = initialData;
            preReadOffset = initialOffset;
            preReadCount = initialSize;
        }

        public PreReadConnection(IConnection innerConnection, int initialOffset, int initialSize)
            : base(innerConnection)
        {
            preReadOffset = initialOffset;
            preReadCount = initialSize;
        }

        public void AddPreReadData(byte[] initialData, int initialOffset, int initialSize)
        {
            if (preReadCount > 0)
            {
                byte[] tempBuffer = preReadData ?? base.Connection.AsyncReadBuffer;
                preReadData = Fx.AllocateByteArray(initialSize + preReadCount);
                Buffer.BlockCopy(tempBuffer, preReadOffset, preReadData, 0, preReadCount);
                Buffer.BlockCopy(initialData, initialOffset, preReadData, preReadCount, initialSize);
                preReadOffset = 0;
                preReadCount += initialSize;
            }
            else
            {
                preReadData = initialData;
                preReadOffset = initialOffset;
                preReadCount = initialSize;
            }
        }

        public override int Read(byte[] buffer, int offset, int size, TimeSpan timeout)
        {
            ConnectionUtilities.ValidateBufferBounds(buffer, offset, size);

            if (preReadCount > 0)
            {
                int bytesToCopy = Math.Min(size, preReadCount);
                Buffer.BlockCopy(base.Connection.AsyncReadBuffer, preReadOffset, buffer, offset, bytesToCopy);
                preReadOffset += bytesToCopy;
                preReadCount -= bytesToCopy;
                return bytesToCopy;
            }

            return base.Read(buffer, offset, size, timeout);
        }

        public override AsyncCompletionResult BeginRead(int offset, int size, TimeSpan timeout, Action<object> callback, object state)
        {
            ConnectionUtilities.ValidateBufferBounds(AsyncReadBufferSize, offset, size);

            if (preReadCount > 0)
            {
                int bytesToCopy = Math.Min(size, preReadCount);
                if (preReadData == null)
                {
                    if (offset != preReadOffset)
                    {
                        preReadData = Fx.AllocateByteArray(preReadCount);
                        Buffer.BlockCopy(base.Connection.AsyncReadBuffer, preReadOffset, preReadData, 0, preReadCount);
                        preReadOffset = 0;
                        Buffer.BlockCopy(preReadData, 0, base.Connection.AsyncReadBuffer, offset, bytesToCopy);
                        preReadOffset += bytesToCopy;
                        preReadCount -= bytesToCopy;
                        asyncBytesRead = bytesToCopy;
                        return AsyncCompletionResult.Completed;
                    }

                    // Requested offset and preReadOffset are the same so no copy needed
                    preReadOffset += bytesToCopy;
                    preReadCount -= bytesToCopy;
                    asyncBytesRead = bytesToCopy;
                    return AsyncCompletionResult.Completed;
                }

                Buffer.BlockCopy(preReadData, preReadOffset, AsyncReadBuffer, offset, bytesToCopy);
                preReadOffset += bytesToCopy;
                preReadCount -= bytesToCopy;
                asyncBytesRead = bytesToCopy;
                return AsyncCompletionResult.Completed;
            }

            return base.BeginRead(offset, size, timeout, callback, state);
        }

        public override int EndRead()
        {
            if (asyncBytesRead > 0)
            {
                int retValue = asyncBytesRead;
                asyncBytesRead = 0;
                return retValue;
            }

            return base.EndRead();
        }
    }

    class ConnectionStream : Stream
    {
        TimeSpan closeTimeout;
        int readTimeout;
        int writeTimeout;
        IConnection connection;
        bool immediate;
        private static Action<object> s_onWriteComplete = OnWriteComplete;
        private static Action<object> s_onReadComplete = OnReadComplete;

        public ConnectionStream(IConnection connection, IDefaultCommunicationTimeouts defaultTimeouts)
        {
            this.connection = connection;
            closeTimeout = defaultTimeouts.CloseTimeout;
            ReadTimeout = TimeoutHelper.ToMilliseconds(defaultTimeouts.ReceiveTimeout);
            WriteTimeout = TimeoutHelper.ToMilliseconds(defaultTimeouts.SendTimeout);
            immediate = true;
        }

        public IConnection Connection
        {
            get { return connection; }
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanTimeout
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public TimeSpan CloseTimeout
        {
            get { return closeTimeout; }
            set { closeTimeout = value; }
        }

        public override int ReadTimeout
        {
            get { return readTimeout; }
            set
            {
                if (value < -1)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.ValueMustBeInRange, -1, int.MaxValue)));
                }

                readTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get { return writeTimeout; }
            set
            {
                if (value < -1)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", value,
                        SR.Format(SR.ValueMustBeInRange, -1, int.MaxValue)));
                }

                writeTimeout = value;
            }
        }

        public bool Immediate
        {
            get { return immediate; }
            set { immediate = value; }
        }

        public override long Length
        {
            get
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
            }
        }

        public override long Position
        {
            get
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
            }
            set
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
            }
        }

        public TraceEventType ExceptionEventType
        {
            get { return connection.ExceptionEventType; }
            set { connection.ExceptionEventType = value; }
        }

        public void Abort()
        {
            connection.Abort();
        }

        public override void Close()
        {
            connection.Close(CloseTimeout, false);
        }

        public override void Flush()
        {
            // NOP
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return WriteAsync(buffer, offset, count).ToApm(callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            asyncResult.ToApmEnd();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>(this);
            var asyncCompletionResult = connection.BeginWrite(buffer, offset, count, Immediate,
                TimeoutHelper.FromMilliseconds(WriteTimeout), s_onWriteComplete, tcs);
            if (asyncCompletionResult == AsyncCompletionResult.Completed)
            {
                connection.EndWrite();
                tcs.TrySetResult(true);
            }

            return tcs.Task;
        }

        private static void OnWriteComplete(object state)
        {
            if (state == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(state));
            }

            var tcs = state as TaskCompletionSource<bool>;
            if (tcs == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("state", SR.SPS_InvalidAsyncResult);
            }

            var thisPtr = tcs.Task.AsyncState as ConnectionStream;
            if (thisPtr == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("state", SR.SPS_InvalidAsyncResult);
            }

            try
            {
                thisPtr.connection.EndWrite();
                tcs.TrySetResult(true);
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            connection.Write(buffer, offset, count, Immediate, TimeoutHelper.FromMilliseconds(WriteTimeout));
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            Contract.Assert(false, $"APM methods shouldn't be used:{new StackFrame()}");
            return ReadAsync(buffer, offset, count, CancellationToken.None).ToApm(callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return asyncResult.ToApmEnd<int>();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<int>(this);
            AsyncCompletionResult asyncCompletionResult = connection.BeginRead(0, Math.Min(count, connection.AsyncReadBufferSize),
                TimeoutHelper.FromMilliseconds(ReadTimeout), s_onReadComplete, tcs);

            if (asyncCompletionResult == AsyncCompletionResult.Completed)
            {
                tcs.TrySetResult(connection.EndRead());
            }

            int bytesRead = await tcs.Task;
            Buffer.BlockCopy(connection.AsyncReadBuffer, 0, buffer, offset, bytesRead);
            return bytesRead;
        }

        private static void OnReadComplete(object state)
        {
            if (state == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(state));
            }

            var tcs = state as TaskCompletionSource<int>;
            if (tcs == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("state", SR.SPS_InvalidAsyncResult);
            }

            var thisPtr = tcs.Task.AsyncState as ConnectionStream;
            if (thisPtr == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument("state", SR.SPS_InvalidAsyncResult);
            }

            try
            {
                tcs.TrySetResult(thisPtr.connection.EndRead());
            }
            catch (Exception e)
            {
                tcs.TrySetException(e);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer, offset, count, TimeoutHelper.FromMilliseconds(ReadTimeout));
        }

        protected int Read(byte[] buffer, int offset, int count, TimeSpan timeout)
        {
            return connection.Read(buffer, offset, count, timeout);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
        }


        public override void SetLength(long value)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
        }

        public void Shutdown(TimeSpan timeout)
        {
            connection.Shutdown(timeout);
        }

        public Task<bool> ValidateAsync(Uri uri)
        {
            return connection.ValidateAsync(uri);
        }
    }

    class StreamConnection : IConnection
    {
        byte[] asyncReadBuffer;
        int bytesRead;
        ConnectionStream innerStream;
        private Action<Task<int>, object> onRead;
        private Action<Task, object> onWrite;
        private Task<int> readResult;
        private Task writeResult;
        Action<object> readCallback;
        Action<object> writeCallback;
        Stream stream;

        public StreamConnection(Stream stream, ConnectionStream innerStream)
        {
            Fx.Assert(stream != null, "StreamConnection: Stream cannot be null.");
            Fx.Assert(innerStream != null, "StreamConnection: Inner stream cannot be null.");

            this.stream = stream;
            this.innerStream = innerStream;

            onRead = Fx.ThunkCallback<Task<int>, object>(OnRead);
            onWrite = Fx.ThunkCallback<Task, object>(OnWrite);
        }

        public byte[] AsyncReadBuffer
        {
            get
            {
                if (asyncReadBuffer == null)
                {
                    lock (ThisLock)
                    {
                        if (asyncReadBuffer == null)
                        {
                            asyncReadBuffer = Fx.AllocateByteArray(innerStream.Connection.AsyncReadBufferSize);
                        }
                    }
                }

                return asyncReadBuffer;
            }
        }

        public int AsyncReadBufferSize
        {
            get { return innerStream.Connection.AsyncReadBufferSize; }
        }

        public Stream Stream
        {
            get { return stream; }
        }

        public object ThisLock
        {
            get { return this; }
        }

        public TraceEventType ExceptionEventType
        {
            get { return innerStream.ExceptionEventType; }
            set { innerStream.ExceptionEventType = value; }
        }

        public IPEndPoint RemoteIPEndPoint
        {
            get
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
            }
        }

        public void Abort()
        {
            innerStream.Abort();
        }

        Exception ConvertIOException(IOException ioException)
        {
            if (ioException.InnerException is TimeoutException)
            {
                return new TimeoutException(ioException.InnerException.Message, ioException);
            }
            else if (ioException.InnerException is CommunicationObjectAbortedException)
            {
                return new CommunicationObjectAbortedException(ioException.InnerException.Message, ioException);
            }
            else if (ioException.InnerException is CommunicationException)
            {
                return new CommunicationException(ioException.InnerException.Message, ioException);
            }
            else
            {
                return new CommunicationException(SR.StreamError, ioException);
            }
        }

        public void Close(TimeSpan timeout, bool asyncAndLinger)
        {
            innerStream.CloseTimeout = timeout;
            try
            {
                stream.Close();
            }
            catch (IOException ioException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(ConvertIOException(ioException));
            }
        }

        public void Shutdown(TimeSpan timeout)
        {
            innerStream.Shutdown(timeout);
        }

        public object DuplicateAndClose(int targetProcessId)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public virtual object GetCoreTransport()
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public Task<bool> ValidateAsync(Uri uri)
        {
            return innerStream.ValidateAsync(uri);
        }

        public AsyncCompletionResult BeginWrite(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout,
            Action<object> callback, object state)
        {
            Contract.Requires(callback != null, "Cannot call BeginWrite without a callback");
            Contract.Requires(writeCallback == null, "BeginWrite cannot be called twice");

            writeCallback = callback;
            bool throwing = true;

            try
            {
                innerStream.Immediate = immediate;
                SetWriteTimeout(timeout);
                Task localTask = stream.WriteAsync(buffer, offset, size);

                if (!localTask.IsCompleted)
                {
                    throwing = false;
                    localTask.ContinueWith(onWrite, state);
                    return AsyncCompletionResult.Queued;
                }

                localTask.GetAwaiter().GetResult();
                throwing = false;
            }
            catch (IOException ioException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(ConvertIOException(ioException));
            }
            finally
            {
                if (throwing)
                {
                    writeCallback = null;
                }
            }

            return AsyncCompletionResult.Completed;
        }

        public void EndWrite()
        {
            IAsyncResult localResult = writeResult;
            writeResult = null;
            writeCallback = null;

            if (localResult != null)
            {
                try
                {
                    stream.EndWrite(localResult);
                }
                catch (IOException ioException)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(ConvertIOException(ioException));
                }
            }
        }

        private void OnWrite(Task antecedant, object state)
        {
            Fx.Assert(writeResult != null, "StreamConnection: OnWrite called twice.");
            writeResult = antecedant;
            writeCallback(state);
        }

        public void Write(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout)
        {
            try
            {
                innerStream.Immediate = immediate;
                SetWriteTimeout(timeout);
                stream.Write(buffer, offset, size);
            }
            catch (IOException ioException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(ConvertIOException(ioException));
            }
        }

        public void Write(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout, BufferManager bufferManager)
        {
            Write(buffer, offset, size, immediate, timeout);
            bufferManager.ReturnBuffer(buffer);
        }

        void SetReadTimeout(TimeSpan timeout)
        {
            int timeoutInMilliseconds = TimeoutHelper.ToMilliseconds(timeout);
            if (stream.CanTimeout)
            {
                stream.ReadTimeout = timeoutInMilliseconds;
            }
            innerStream.ReadTimeout = timeoutInMilliseconds;
        }

        void SetWriteTimeout(TimeSpan timeout)
        {
            int timeoutInMilliseconds = TimeoutHelper.ToMilliseconds(timeout);
            if (stream.CanTimeout)
            {
                stream.WriteTimeout = timeoutInMilliseconds;
            }
            innerStream.WriteTimeout = timeoutInMilliseconds;
        }

        public int Read(byte[] buffer, int offset, int size, TimeSpan timeout)
        {
            try
            {
                SetReadTimeout(timeout);
                return stream.Read(buffer, offset, size);
            }
            catch (IOException ioException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(ConvertIOException(ioException));
            }
        }

        public AsyncCompletionResult BeginRead(int offset, int size, TimeSpan timeout, Action<object> callback, object state)
        {
            ConnectionUtilities.ValidateBufferBounds(AsyncReadBufferSize, offset, size);
            readCallback = callback;

            try
            {
                SetReadTimeout(timeout);
                Task<int> localTask = stream.ReadAsync(AsyncReadBuffer, offset, size);

                if (!localTask.IsCompleted)
                {
                    localTask.ContinueWith(onRead, state);
                    return AsyncCompletionResult.Queued;
                }

                bytesRead = localTask.GetAwaiter().GetResult();
            }
            catch (IOException ioException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(ConvertIOException(ioException));
            }

            return AsyncCompletionResult.Completed;
        }

        public int EndRead()
        {
            Task<int> localResult = readResult;
            readResult = null;

            if (localResult != null)
            {
                try
                {
                    bytesRead = localResult.GetAwaiter().GetResult();
                }
                catch (IOException ioException)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(ConvertIOException(ioException));
                }
            }

            return bytesRead;
        }

        void OnRead(Task<int> antecedant, object state)
        {
            Fx.Assert(readResult != null, "StreamConnection: OnRead called twice.");
            readResult = antecedant;
            readCallback(state);
        }
    }

    class ConnectionMessageProperty
    {
        IConnection connection;

        public ConnectionMessageProperty(IConnection connection)
        {
            this.connection = connection;
        }

        public static string Name
        {
            get { return "iconnection"; }
        }

        public IConnection Connection
        {
            get { return connection; }
        }
    }

    static class ConnectionUtilities
    {
        internal static void CloseNoThrow(IConnection connection, TimeSpan timeout)
        {
            bool success = false;
            try
            {
                // TODO: Change IConnection.Close to async and switch to async here
                connection.Close(timeout, false);
                success = true;
            }
            catch (TimeoutException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (CommunicationException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            finally
            {
                if (!success)
                {
                    connection.Abort();
                }
            }
        }

        internal static void ValidateBufferBounds(ArraySegment<byte> buffer)
        {
            ValidateBufferBounds(buffer.Array, buffer.Offset, buffer.Count);
        }

        internal static void ValidateBufferBounds(byte[] buffer, int offset, int size)
        {
            if (buffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(buffer));
            }

            ValidateBufferBounds(buffer.Length, offset, size);
        }

        internal static void ValidateBufferBounds(int bufferSize, int offset, int size)
        {
            if (offset < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("offset", offset, SR.ValueMustBeNonNegative));
            }

            if (offset > bufferSize)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("offset", offset, SR.Format(
                    SR.OffsetExceedsBufferSize, bufferSize)));
            }

            if (size <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("size", size, SR.ValueMustBePositive));
            }

            int remainingBufferSpace = bufferSize - offset;
            if (size > remainingBufferSpace)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("size", size, SR.Format(
                    SR.SizeExceedsRemainingBufferSpace, remainingBufferSpace)));
            }
        }
    }
}
