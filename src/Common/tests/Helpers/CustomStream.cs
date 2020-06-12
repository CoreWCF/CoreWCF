using System;
using System.IO;
using System.Threading;

namespace Helpers
{  
    // Custom stream class returns the repeating pattern over and over, until its max size is reached.
    public class CustomStream : Stream
    {
        private byte[] repeatingPattern;
        private long maxBytesToReturn;
        private long bytesAlreadyReturned;
        private bool sleepyStream;

        public CustomStream(byte[] pattern, long maxBytesToReturn, bool sleepyStream)
        {
            this.repeatingPattern = pattern;
            this.maxBytesToReturn = maxBytesToReturn;
            this.bytesAlreadyReturned = 0;
            this.sleepyStream = sleepyStream;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
        }

        public override long Length
        {
            get { return maxBytesToReturn; }
        }

        public override long Position
        {
            get
            {
                return bytesAlreadyReturned;
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            int bytesLeftToRead = count;
            int bytesRead = 0;
            long maxBytesToRead = this.maxBytesToReturn - bytesAlreadyReturned;
            if (((long)bytesLeftToRead) > maxBytesToRead)
            {
                bytesLeftToRead = (int)maxBytesToRead;
            }

            bytesRead = CustomRead(bytesLeftToRead, bytesRead, buffer, offset);
            return new SleepyAsyncResult(bytesRead, this.sleepyStream ? 100 : 0, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return SleepyAsyncResult.End(asyncResult);
        }

        public int CustomRead(int bytesLeftToRead, int bytesRead, byte[] buffer, int offset)
        {
            while (bytesLeftToRead > 0)
            {
                int offsetIntoPattern = (int)((bytesAlreadyReturned + bytesRead) % repeatingPattern.Length);
                int bytesLeftFromPattern = repeatingPattern.Length - offsetIntoPattern;
                if (bytesLeftToRead < bytesLeftFromPattern)
                {
                    Array.Copy(repeatingPattern, offsetIntoPattern, buffer, offset + bytesRead, bytesLeftToRead);
                    bytesRead += bytesLeftToRead;
                    bytesLeftToRead = 0;
                }
                else
                {
                    Array.Copy(repeatingPattern, offsetIntoPattern, buffer, offset + bytesRead, bytesLeftFromPattern);
                    bytesRead += bytesLeftFromPattern;
                    bytesLeftToRead -= bytesLeftFromPattern;
                }
            }

            bytesAlreadyReturned += bytesRead;
            return bytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new ApplicationException("Sync Path should not be called");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotSupportedException();
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            throw new NotSupportedException();
        }
    }

    public class SleepyAsyncResult : TypedAsyncResult<int>
    {
        private Timer timer;
        private int bytesRead;

        public SleepyAsyncResult(int bytesRead, int delayInMilliseconds, AsyncCallback callback, object state)
            : base(callback, state)
        {
            if (delayInMilliseconds < 0)
            {
                throw new ArgumentException("value must be non-negative", "delayInMilliseconds");
            }

            if (bytesRead < 0)
            {
                throw new ArgumentException("value must be non-negative", "bytesRead");
            }

            this.timer = new Timer(new TimerCallback(this.OnTimerFire), null, Timeout.Infinite, Timeout.Infinite);
            this.timer.Change(delayInMilliseconds, Timeout.Infinite);
            this.bytesRead = bytesRead;
        }

        private void OnTimerFire(object state)
        {
            Complete(this.bytesRead, false);
            this.timer.Dispose();
        }
    }

    //A strongly typed AsyncResult
    public abstract class TypedAsyncResult<T> : AsyncResult
    {
        private T data;

        protected TypedAsyncResult(AsyncCallback callback, object state)
            : base(callback, state)
        {
        }

        public T Data
        {
            get { return this.data; }
        }

        protected void Complete(T data, bool completedSynchronously)
        {
            this.data = data;
            Complete(completedSynchronously);
        }

        public static T End(IAsyncResult result)
        {
            TypedAsyncResult<T> typedResult = AsyncResult.End<TypedAsyncResult<T>>(result);
            return typedResult.Data;
        }
    }

    public abstract class AsyncResult : IAsyncResult
    {
        static AsyncCallback asyncCompletionWrapperCallback;
        AsyncCallback callback;
        bool completedSynchronously;
        bool endCalled;
        Exception exception;
        bool isCompleted;
        ManualResetEvent manualResetEvent;
        AsyncCompletion nextAsyncCompletion;
        object state;
        object thisLock;

#if DEBUG_EXPENSIVE
        StackTrace endStack;
        StackTrace completeStack;
#endif

        protected AsyncResult(AsyncCallback callback, object state)
        {
            this.callback = callback;
            this.state = state;
            this.thisLock = new object();
        }

        public object AsyncState
        {
            get
            {
                return state;
            }
        }

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                if (manualResetEvent != null)
                {
                    return manualResetEvent;
                }

                lock (ThisLock)
                {
                    if (manualResetEvent == null)
                    {
                        manualResetEvent = new ManualResetEvent(isCompleted);
                    }
                }

                return manualResetEvent;
            }
        }

        public bool CompletedSynchronously
        {
            get
            {
                return completedSynchronously;
            }
        }

        public bool HasCallback
        {
            get
            {
                return this.callback != null;
            }
        }

        public bool IsCompleted
        {
            get
            {
                return isCompleted;
            }
        }

        object ThisLock
        {
            get
            {
                return this.thisLock;
            }
        }

        protected void Complete(bool completedSynchronously)
        {
            if (isCompleted)
            {
                // It's a bug to call Complete twice.
                System.Diagnostics.Debug.Assert(false, "AsyncResult complete called twice for the same operation.");
                throw new InvalidProgramException();
            }

#if DEBUG_EXPENSIVE
            if (completeStack == null)
                completeStack = new StackTrace();
#endif

            this.completedSynchronously = completedSynchronously;

            if (completedSynchronously)
            {
                // If we completedSynchronously, then there's no chance that the manualResetEvent was created so
                // we don't need to worry about a race
                System.Diagnostics.Debug.Assert(this.manualResetEvent == null, "No ManualResetEvent should be created for a synchronous AsyncResult.");
                this.isCompleted = true;
            }
            else
            {
                lock (ThisLock)
                {
                    this.isCompleted = true;
                    if (this.manualResetEvent != null)
                    {
                        this.manualResetEvent.Set();
                    }
                }
            }

            if (callback != null)
            {
                try
                {
                    callback(this);
                }
#pragma warning disable 1634
#pragma warning suppress 56500 // transferring exception to another thread
                catch (Exception e)
                {
                    throw new InvalidProgramException("Async Callback threw an exception.", e);
                }
#pragma warning restore 1634
            }
        }

        protected void Complete(bool completedSynchronously, Exception exception)
        {
            this.exception = exception;
            Complete(completedSynchronously);
        }

        static void AsyncCompletionWrapperCallback(IAsyncResult result)
        {
            if (result.CompletedSynchronously)
            {
                return;
            }

            AsyncResult thisPtr = (AsyncResult)result.AsyncState;
            AsyncCompletion callback = thisPtr.GetNextCompletion();

            bool completeSelf = false;
            Exception completionException = null;
            try
            {
                completeSelf = callback(result);
            }
            catch (Exception e)
            {
                completeSelf = true;
                completionException = e;
            }

            if (completeSelf)
            {
                thisPtr.Complete(false, completionException);
            }
        }

        protected AsyncCallback PrepareAsyncCompletion(AsyncCompletion callback)
        {
            this.nextAsyncCompletion = callback;
            if (AsyncResult.asyncCompletionWrapperCallback == null)
            {
                AsyncResult.asyncCompletionWrapperCallback = new AsyncCallback(AsyncCompletionWrapperCallback);
            }
            return AsyncResult.asyncCompletionWrapperCallback;
        }

        AsyncCompletion GetNextCompletion()
        {
            AsyncCompletion result = this.nextAsyncCompletion;
            System.Diagnostics.Debug.Assert(result != null, "next async completion should be non-null");
            this.nextAsyncCompletion = null;
            return result;
        }

        protected static TAsyncResult End<TAsyncResult>(IAsyncResult result)
            where TAsyncResult : AsyncResult
        {
            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            TAsyncResult asyncResult = result as TAsyncResult;

            if (asyncResult == null)
            {
                throw new ArgumentException("result", "Invalid async result.");
            }

            if (asyncResult.endCalled)
            {
                throw new InvalidOperationException("End cannot be called twice on an AsyncResult.");
            }

#if DEBUG_EXPENSIVE
            if (asyncResult.endStack == null)
                asyncResult.endStack = new StackTrace();
#endif

            asyncResult.endCalled = true;

            if (!asyncResult.isCompleted)
            {
                asyncResult.AsyncWaitHandle.WaitOne();
            }

            if (asyncResult.manualResetEvent != null)
            {
                asyncResult.manualResetEvent.Close();
            }

            if (asyncResult.exception != null)
            {
                throw asyncResult.exception;
            }

            return asyncResult;
        }

        // can be utilized by subclasses to write core completion code for both the sync and async paths
        // in one location, signalling chainable synchronous completion with the boolean result,
        // and leveraging PrepareAsyncCompletion for conversion to an AsyncCallback.
        // NOTE: requires that "this" is passed in as the state object to the asynchronous sub-call being used with a completion routine.
        protected delegate bool AsyncCompletion(IAsyncResult result);
    }
}

