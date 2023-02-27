// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Runtime
{
    internal static class TaskHelpers
    {
        // Helper method when implementing an APM wrapper around a Task based async method which returns a result.
        // In the BeginMethod method, you would call use ToApm to wrap a call to MethodAsync:
        //     return MethodAsync(params).ToApm(callback, state);
        // In the EndMethod, you would use ToApmEnd<TResult> to ensure the correct exception handling
        // This will handle throwing exceptions in the correct place and ensure the IAsyncResult contains the provided
        // state object
        public static IAsyncResult ToApm<T>(this Task<T> task, AsyncCallback callback, object state)
            => ToApm<T>(new ValueTask<T>(task), callback, state);

        /// <summary>
        /// Helper method to convert from Task async method to "APM" (IAsyncResult with Begin/End calls)
        /// </summary>
        public static IAsyncResult ToApm<T>(this ValueTask<T> valueTask, AsyncCallback callback, object state)
        {
            var result = new AsyncResult<T>(valueTask, callback, state);
            if (result.CompletedSynchronously)
            {
                result.ExecuteCallback();
            }
            else if (callback != null)
            {
                // We use OnCompleted rather than ContinueWith in order to avoid running synchronously
                // if the task has already completed by the time we get here.
                // This will allocate a delegate and some extra data to add it as a TaskContinuation
                valueTask.ConfigureAwait(false)
                    .GetAwaiter()
                    .OnCompleted(result.ExecuteCallback);
            }

            return result;
        }

        /// <summary>
        /// Helper method to convert from Task async method to "APM" (IAsyncResult with Begin/End calls)
        /// </summary>
        public static IAsyncResult ToApm(this Task task, AsyncCallback callback, object state)
        {
            var result = new AsyncResult(task, callback, state);
            if (result.CompletedSynchronously)
            {
                result.ExecuteCallback();
            }
            else if (callback != null)
            {
                // We use OnCompleted rather than ContinueWith in order to avoid running synchronously
                // if the task has already completed by the time we get here.
                // This will allocate a delegate and some extra data to add it as a TaskContinuation
                task.ConfigureAwait(false)
                    .GetAwaiter()
                    .OnCompleted(result.ExecuteCallback);
            }

            return result;
        }

        public static T ToApmEnd<T>(this IAsyncResult asyncResult)
        {
            if (asyncResult is AsyncResult<T> asyncResultInstance)
            {
                return asyncResultInstance.GetResult();
            }
            else
            {
                // throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                //     new ArgumentException(SRCommon.SFxInvalidCallbackIAsyncResult));
                throw new ArgumentException(nameof(asyncResult));
            }
        }

        public static void ToApmEnd(this IAsyncResult asyncResult)
        {
            if (asyncResult is AsyncResult asyncResultInstance)
            {
                asyncResultInstance.GetResult();
            }
            else
            {
                // throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                //     new ArgumentException(SRCommon.SFxInvalidCallbackIAsyncResult));
                throw new ArgumentException(nameof(asyncResult));
            }
        }

        private class AsyncResult : IAsyncResult
        {
            private readonly Task _task;
            private readonly AsyncCallback _asyncCallback;

            public AsyncResult(Task task, AsyncCallback asyncCallback, object asyncState)
            {
                _task = task;
                _asyncCallback = asyncCallback;
                AsyncState = asyncState;
                CompletedSynchronously = task.IsCompleted;
            }

            public void GetResult() => _task.GetAwaiter().GetResult();

            public void ExecuteCallback() => _asyncCallback?.Invoke(this);

            public object AsyncState { get; }
            WaitHandle IAsyncResult.AsyncWaitHandle => ((IAsyncResult)_task).AsyncWaitHandle;

            public bool CompletedSynchronously { get; }
            public bool IsCompleted => _task.IsCompleted;
        }

        internal class AsyncResult<T> : IAsyncResult
        {
            private readonly ValueTask<T> _task;
            private readonly AsyncCallback _asyncCallback;

            public AsyncResult(ValueTask<T> task, AsyncCallback asyncCallback, object asyncState)
            {
                _task = task;
                _asyncCallback = asyncCallback;
                AsyncState = asyncState;
                CompletedSynchronously = task.IsCompleted;
            }

            public T GetResult() => _task.GetAwaiter().GetResult();

            public bool IsFaulted => _task.IsFaulted;
            public AggregateException Exception => _task.AsTask().Exception;

            // Calls the async callback with this as parameter
            public void ExecuteCallback() => _asyncCallback?.Invoke(this);
            public object AsyncState { get; }

            WaitHandle IAsyncResult.AsyncWaitHandle => !CompletedSynchronously
                ? ((IAsyncResult)_task.AsTask()).AsyncWaitHandle
                : throw new NotImplementedException();

            public bool CompletedSynchronously { get; }
            public bool IsCompleted => _task.IsCompleted;
        }
    }
}
