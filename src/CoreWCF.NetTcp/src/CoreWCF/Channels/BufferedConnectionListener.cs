using System;
using CoreWCF.Runtime;
using CoreWCF;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    class BufferedConnection : DelegatingConnection
    {
        byte[] writeBuffer;
        int writeBufferSize;
        int pendingWriteSize;
        Exception pendingWriteException;
        IOThreadTimer flushTimer;
        long flushTimeout;
        TimeSpan pendingTimeout;
        const int maxFlushSkew = 100;

        public BufferedConnection(IConnection connection, TimeSpan flushTimeout, int writeBufferSize)
            : base(connection)
        {
            this.flushTimeout = Ticks.FromTimeSpan(flushTimeout);
            this.writeBufferSize = writeBufferSize;
        }

        object ThisLock
        {
            get { return this; }
        }

        public override void Close(TimeSpan timeout, bool asyncAndLinger)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            Flush(timeoutHelper.RemainingTime());
            base.Close(timeoutHelper.RemainingTime(), asyncAndLinger);
        }

        void CancelFlushTimer()
        {
            if (flushTimer != null)
            {
                flushTimer.Cancel();
                pendingTimeout = TimeSpan.Zero;
            }
        }

        void Flush(TimeSpan timeout)
        {
            ThrowPendingWriteException();

            lock (ThisLock)
            {
                FlushCore(timeout);
            }
        }

        void FlushCore(TimeSpan timeout)
        {
            if (pendingWriteSize > 0)
            {
                Connection.Write(writeBuffer, 0, pendingWriteSize, false, timeout);
                pendingWriteSize = 0;
            }
        }

        void OnFlushTimer(object state)
        {
            lock (ThisLock)
            {
                try
                {
                    FlushCore(pendingTimeout);
                    pendingTimeout = TimeSpan.Zero;
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    pendingWriteException = e;
                    CancelFlushTimer();
                }
            }
        }

        void SetFlushTimer()
        {
            if (flushTimer == null)
            {
                int flushSkew = Ticks.ToMilliseconds(Math.Min(flushTimeout / 10, Ticks.FromMilliseconds(maxFlushSkew)));
                flushTimer = new IOThreadTimer(new Action<object>(OnFlushTimer), null, true, flushSkew);
            }
            flushTimer.Set(Ticks.ToTimeSpan(flushTimeout));
        }

        public override void Write(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout, BufferManager bufferManager)
        {
            if (size <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("size", size, SR.ValueMustBePositive));
            }

            ThrowPendingWriteException();

            if (immediate || flushTimeout == 0)
            {
                WriteNow(buffer, offset, size, timeout, bufferManager);
            }
            else
            {
                WriteLater(buffer, offset, size, timeout);
                bufferManager.ReturnBuffer(buffer);
            }
        }

        public override void Write(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout)
        {
            if (size <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("size", size, SR.ValueMustBePositive));
            }

            ThrowPendingWriteException();

            if (immediate || flushTimeout == 0)
            {
                WriteNow(buffer, offset, size, timeout);
            }
            else
            {
                WriteLater(buffer, offset, size, timeout);
            }
        }

        void WriteNow(byte[] buffer, int offset, int size, TimeSpan timeout)
        {
            WriteNow(buffer, offset, size, timeout, null);
        }

        void WriteNow(byte[] buffer, int offset, int size, TimeSpan timeout, BufferManager bufferManager)
        {
            lock (ThisLock)
            {
                if (pendingWriteSize > 0)
                {
                    int remainingSize = writeBufferSize - pendingWriteSize;
                    CancelFlushTimer();
                    if (size <= remainingSize)
                    {
                        Buffer.BlockCopy(buffer, offset, writeBuffer, pendingWriteSize, size);
                        if (bufferManager != null)
                        {
                            bufferManager.ReturnBuffer(buffer);
                        }
                        pendingWriteSize += size;
                        FlushCore(timeout);
                        return;
                    }
                    else
                    {
                        TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
                        FlushCore(timeoutHelper.RemainingTime());
                        timeout = timeoutHelper.RemainingTime();
                    }
                }

                if (bufferManager == null)
                {
                    Connection.Write(buffer, offset, size, true, timeout);
                }
                else
                {
                    Connection.Write(buffer, offset, size, true, timeout, bufferManager);
                }
            }
        }

        void WriteLater(byte[] buffer, int offset, int size, TimeSpan timeout)
        {
            lock (ThisLock)
            {
                bool setTimer = (pendingWriteSize == 0);
                TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);

                while (size > 0)
                {
                    if (size >= writeBufferSize && pendingWriteSize == 0)
                    {
                        Connection.Write(buffer, offset, size, false, timeoutHelper.RemainingTime());
                        size = 0;
                    }
                    else
                    {
                        if (writeBuffer == null)
                        {
                            writeBuffer = Fx.AllocateByteArray(writeBufferSize);
                        }

                        int remainingSize = writeBufferSize - pendingWriteSize;
                        int copySize = size;
                        if (copySize > remainingSize)
                        {
                            copySize = remainingSize;
                        }

                        Buffer.BlockCopy(buffer, offset, writeBuffer, pendingWriteSize, copySize);
                        pendingWriteSize += copySize;
                        if (pendingWriteSize == writeBufferSize)
                        {
                            FlushCore(timeoutHelper.RemainingTime());
                            setTimer = true;
                        }
                        size -= copySize;
                        offset += copySize;
                    }
                }
                if (pendingWriteSize > 0)
                {
                    if (setTimer)
                    {
                        SetFlushTimer();
                        pendingTimeout = TimeoutHelper.Add(pendingTimeout, timeoutHelper.RemainingTime());
                    }
                }
                else
                {
                    CancelFlushTimer();
                }
            }
        }

        public override AsyncCompletionResult BeginWrite(byte[] buffer, int offset, int size, bool immediate, TimeSpan timeout,
            Action<object> callback, object state)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            Flush(timeoutHelper.RemainingTime());
            return base.BeginWrite(buffer, offset, size, immediate, timeoutHelper.RemainingTime(), callback, state);
        }

        public override void EndWrite()
        {
            base.EndWrite();
        }

        public override void Shutdown(TimeSpan timeout)
        {
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            Flush(timeoutHelper.RemainingTime());
            base.Shutdown(timeoutHelper.RemainingTime());
        }

        void ThrowPendingWriteException()
        {
            if (pendingWriteException != null)
            {
                lock (ThisLock)
                {
                    if (pendingWriteException != null)
                    {
                        Exception exceptionTothrow = pendingWriteException;
                        pendingWriteException = null;
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(exceptionTothrow);
                    }
                }
            }
        }
    }

    class BufferedConnectionListener : IConnectionListener
    {
        int writeBufferSize;
        TimeSpan flushTimeout;
        IConnectionListener connectionListener;

        public BufferedConnectionListener(IConnectionListener connectionListener, TimeSpan flushTimeout, int writeBufferSize)
        {
            this.connectionListener = connectionListener;
            this.flushTimeout = flushTimeout;
            this.writeBufferSize = writeBufferSize;
        }

        public void Dispose()
        {
            connectionListener.Dispose();
        }

        public void Listen()
        {
            connectionListener.Listen();
        }

        public async Task<IConnection> AcceptAsync()
        {
            return new BufferedConnection(await connectionListener.AcceptAsync(), flushTimeout, writeBufferSize);

        }
    }
}
