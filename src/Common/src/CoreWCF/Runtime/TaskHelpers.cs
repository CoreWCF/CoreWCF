// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Dispatcher;

namespace CoreWCF.Runtime
{
    internal static class TaskHelpers
    {
        //This replaces the Wait<TException>(this Task task) method as we want to await and not Wait()
        public static async Task AsyncWait<TException>(this Task task)
        {
            try
            {
                await task;
            }
            catch
            {
                throw Fx.Exception.AsError<TException>(task.Exception);
            }
        }

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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SRCommon.SFxInvalidCallbackIAsyncResult));
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SRCommon.SFxInvalidCallbackIAsyncResult));
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
            WaitHandle IAsyncResult.AsyncWaitHandle => !CompletedSynchronously ? ((IAsyncResult)_task.AsTask()).AsyncWaitHandle : throw new NotImplementedException();

            public bool CompletedSynchronously { get; }
            public bool IsCompleted => _task.IsCompleted;
        }

        // Awaitable helper to await a maximum amount of time for a task to complete. If the task doesn't
        // complete in the specified amount of time, returns false. This does not modify the state of the
        // passed in class, but instead is a mechanism to allow interrupting awaiting a task if a timeout
        // period passes.
        public static async Task<bool> AwaitWithTimeout(this Task task, TimeSpan timeout)
        {
            if (task.IsCompleted)
            {
                return true;
            }

            if (timeout == TimeSpan.MaxValue || timeout == Timeout.InfiniteTimeSpan)
            {
                await task;
                return true;
            }

            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Task completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
                if (completedTask == task)
                {
                    cts.Cancel();
                    return true;
                }
                else
                {
                    return (task.IsCompleted);
                }
            }
        }

        // Task.GetAwaiter().GetResult() calls an internal variant of Wait() which doesn't wrap exceptions in
        // an AggregateException. It does spinwait so if it's expected that the Task isn't about to complete,
        // then use the NoSpin variant.
        public static void WaitForCompletion(this Task task)
        {
            task.GetAwaiter().GetResult();
        }

        // If the task is about to complete, this method will be more expensive than the regular method as it
        // always causes a WaitHandle to be allocated. If it is expected that the task will take longer than
        // the time of a spin wait, then a WaitHandle will be allocated anyway and this method avoids the CPU
        // cost of the spin wait.
        public static void WaitForCompletionNoSpin(this Task task)
        {
            if (!task.IsCompleted)
            {
                ((IAsyncResult)task).AsyncWaitHandle.WaitOne();
            }

            // Call GetResult() to get any exceptions that were thrown
            task.GetAwaiter().GetResult();
        }

        public static TResult WaitForCompletion<TResult>(this Task<TResult> task)
        {
            return task.GetAwaiter().GetResult();
        }

        public static TResult WaitForCompletionNoSpin<TResult>(this Task<TResult> task)
        {
            if (!task.IsCompleted)
            {
                ((IAsyncResult)task).AsyncWaitHandle.WaitOne();
            }

            return task.GetAwaiter().GetResult();
        }

        public static bool WaitForCompletionNoSpin(this Task task, TimeSpan timeout)
        {
            if (timeout >= TimeoutHelper.MaxWait)
            {
                task.WaitForCompletionNoSpin();
                return true;
            }

            bool completed = true;
            if (!task.IsCompleted)
            {
                completed = ((IAsyncResult)task).AsyncWaitHandle.WaitOne(timeout);
            }

            if (completed)
            {
                // Throw any exceptions if there are any
                task.GetAwaiter().GetResult();
            }

            return completed;
        }

        // Used by WebSocketTransportDuplexSessionChannel on the sync code path.
        // TODO: Try and switch as many code paths as possible which use this to async
        public static void Wait(this Task task, TimeSpan timeout, Action<Exception, TimeSpan, string> exceptionConverter, string operationType)
        {
            bool timedOut = false;

            try
            {
                timedOut = !task.WaitForCompletionNoSpin(timeout);
            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex) || exceptionConverter == null)
                {
                    throw;
                }

                exceptionConverter(ex, timeout, operationType);
            }

            if (timedOut)
            {
                throw Fx.Exception.AsError(new TimeoutException(SRCommon.Format(SRCommon.TaskTimedOutError, timeout)));
            }
        }

        public static Task CompletedTask()
        {
            return Task.FromResult(true);
        }

        public static DefaultTaskSchedulerAwaiter EnsureDefaultTaskScheduler()
        {
            return DefaultTaskSchedulerAwaiter.Singleton;
        }

        public static Action<object> OnAsyncCompletionCallback = OnAsyncCompletion;

        // Method to act as callback for asynchronous code which uses AsyncCompletionResult as the return type when used within
        // a Task based async method. These methods require a callback which is called in the case of the IO completing asynchronously.
        // This pattern still requires an allocation, whereas the purpose of using the AsyncCompletionResult enum is to avoid allocation.
        // In the future, this pattern should be replaced with a reusable awaitable object, potentially with a global pool.
        private static void OnAsyncCompletion(object state)
        {
            var tcs = state as TaskCompletionSource<bool>;
            Fx.Assert(tcs != null, "Async state should be of type TaskCompletionSource<bool>");
            tcs.TrySetResult(true);
        }

        public static IDisposable RunTaskContinuationsOnOurThreads()
        {
            if (SynchronizationContext.Current == ServiceModelSynchronizationContext.Instance)
            {
                return null; // No need to save and restore state as we're already using the correct sync context
            }

            return new SyncContextScope();
        }

        // Calls the given Action asynchronously.
        public static async Task CallActionAsync<TArg>(Action<TArg> action, TArg argument)
        {
            using (IDisposable scope = RunTaskContinuationsOnOurThreads())
            {
                if (scope != null)  // No need to change threads if already off of thread pool
                {
                    await Task.Yield(); // Move synchronous method off of thread pool
                }

                action(argument);
            }
        }

        public static Task CompletedOrCanceled(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled(token);
            }

            return Task.CompletedTask;
        }

        private class SyncContextScope : IDisposable
        {
            private readonly SynchronizationContext _prevContext;

            public SyncContextScope()
            {
                _prevContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(ServiceModelSynchronizationContext.Instance);
            }

            public void Dispose()
            {
                SynchronizationContext.SetSynchronizationContext(_prevContext);
            }
        }

        public static Task<TResult> CancellableAsyncWait<TResult>(this Task<TResult> task, CancellationToken token)
        {
            if (!token.CanBeCanceled)
            {
                return task;
            }

            object[] state = new object[2];
            var tcs = new TaskCompletionSource<TResult>(state);
            state[0] = tcs;
            CancellationTokenRegistration registration = token.Register(OnCancellation<TResult>, state);
            state[1] = registration;
            task.ContinueWith((antecedent, obj) =>
            {
                object[] stateArr = (object[])obj;
                var tcsObj = (TaskCompletionSource<TResult>)stateArr[0];
                var tokenRegistration = (CancellationTokenRegistration)stateArr[1];
                tokenRegistration.Dispose();
                if (antecedent.IsFaulted)
                {
                    tcsObj.TrySetException(antecedent.Exception.InnerException);
                }
                else if (antecedent.IsCanceled)
                {
                    tcsObj.TrySetCanceled();
                }
                else
                {
                    tcsObj.TrySetResult(antecedent.Result);
                }
            }, state, CancellationToken.None, TaskContinuationOptions.HideScheduler, TaskScheduler.Default);

            return tcs.Task;
        }

        private static void OnCancellation<TResult>(object state)
        {
            object[] stateArr = (object[])state;
            var tcsObj = (TaskCompletionSource<TResult>)stateArr[0];
            var tokenRegistration = (CancellationTokenRegistration)stateArr[1];
            tcsObj.TrySetCanceled();
            tokenRegistration.Dispose();
        }

        internal static SynchronizationContextAwaiter GetAwaiter(this SynchronizationContext syncContext)
        {
            return new SynchronizationContextAwaiter(syncContext);
        }
    }

    // This awaiter causes an awaiting async method to continue on the same thread if using the
    // default task scheduler, otherwise it posts the continuation to the ThreadPool. While this
    // does a similar function to Task.ConfigureAwait, this code doesn't require a Task to function.
    // With Task.ConfigureAwait, you would need to call it on the first task on each potential code
    // path in a method. This could mean calling ConfigureAwait multiple times in a single method.
    // This awaiter can be awaited on at the beginning of a method a single time and isn't dependant
    // on running other awaitable code.
    internal struct DefaultTaskSchedulerAwaiter : INotifyCompletion
    {
        public static DefaultTaskSchedulerAwaiter Singleton = new DefaultTaskSchedulerAwaiter();

        // If the current TaskScheduler is the default, if we aren't currently running inside a task and
        // the default SynchronizationContext isn't current, when a Task starts, it will change the TaskScheduler
        // to one based off the current SynchronizationContext. Also, any async api's that WCF consumes will
        // post back to the same SynchronizationContext as they were started in which could cause WCF to deadlock
        // on our Sync code path.
        public bool IsCompleted
        {
            get
            {
                return (TaskScheduler.Current == TaskScheduler.Default) &&
                    (SynchronizationContext.Current == null ||
                    (SynchronizationContext.Current.GetType() == typeof(SynchronizationContext)));
            }
        }

        // Only called when IsCompleted returns false, otherwise the caller will call the continuation
        // directly causing it to stay on the same thread.
        public void OnCompleted(Action continuation)
        {
            Task.Run(continuation);
        }

        // Awaiter is only used to control where subsequent awaitable's run so GetResult needs no
        // implementation. Normally any exceptions would be thrown here, but we have nothing to throw
        // as we don't run anything, only control where other code runs.
        public void GetResult() { }

        public DefaultTaskSchedulerAwaiter GetAwaiter()
        {
            return this;
        }
    }

    internal struct SynchronizationContextAwaiter : INotifyCompletion
    {
        private readonly SynchronizationContext _syncContext;

        public SynchronizationContextAwaiter(SynchronizationContext syncContext)
        {
            _syncContext = syncContext;
        }

        public bool IsCompleted => _syncContext == SynchronizationContext.Current;

        public void OnCompleted(Action continuation)
        {
            // _syncContext will be null if it's the default sync context
            // This method is being called because IsCompleted returned false
            // This means we need to run the continuation on a regular thread pool
            // thread.
            if (_syncContext == null)
            {
                ThreadPool.UnsafeQueueUserWorkItem(PostCallback, continuation);
                return;
            }

            _syncContext.Post(PostCallback, continuation);
        }


        public void GetResult() { }

        internal static void PostCallback(object state)
        {
            ((Action)state)();
        }
    }

    // Async methods can't take an out (or ref) argument. This wrapper allows passing in place of an out argument
    // and can be used to return a value via a method argument.
    internal class OutWrapper<T>
    {
        public OutWrapper()
        {
            Value = default;
        }

        public T Value { get; set; }

        public static implicit operator T(OutWrapper<T> wrapper)
        {
            return wrapper.Value;
        }
    }
}