using System;
using System.Reflection;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using System.Diagnostics;

namespace CoreWCF.Dispatcher
{
    internal class TaskMethodInvoker : IOperationInvoker
    {
        private const string ResultMethodName = "Result";
        private readonly MethodInfo taskMethod;
        private InvokeDelegate invokeDelegate;
        private int inputParameterCount;
        private int outputParameterCount;
        private MethodInfo taskTResultGetMethod;
        private bool isGenericTask;

        public TaskMethodInvoker(MethodInfo taskMethod, Type taskType)
        {
            if (taskMethod == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(taskMethod));
            }

            this.taskMethod = taskMethod;

            if (taskType != ServiceReflector.VoidType)
            {
                taskTResultGetMethod = ((PropertyInfo)taskMethod.ReturnType.GetMember(ResultMethodName)[0]).GetGetMethod();
                isGenericTask = true;
            }
        }

        public bool IsSynchronous
        {
            get { return false; }
        }

        public MethodInfo TaskMethod
        {
            get { return taskMethod; }
        }

        public object[] AllocateInputs()
        {
            EnsureIsInitialized();

            return EmptyArray<object>.Allocate(inputParameterCount);
        }

        public object Invoke(object instance, object[] inputs, out object[] outputs)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public IAsyncResult InvokeBegin(object instance, object[] inputs, AsyncCallback callback, object state)
        {
            return ToApm(InvokeAsync(instance, inputs), callback, state);
        }

        public object InvokeEnd(object instance, out object[] outputs, IAsyncResult result)
        {
            if (instance == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxNoServiceObject));
            }

            object returnVal = null;
            //bool callFailed = true;
            //bool callFaulted = false;
            //ServiceModelActivity activity = null;
            //Activity boundOperation = null;

            try
            {
                //AsyncMethodInvoker.GetActivityInfo(ref activity, ref boundOperation);

                var asyncResult = result as TaskHelpers.AsyncResult<Tuple<object, object[]>>;
                if (asyncResult == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxInvalidCallbackIAsyncResult);
                }

                AggregateException ae = null;
                Tuple<object, object[]> tuple = null;
                Task task = null;

                if (asyncResult.IsFaulted)
                {
                    Fx.Assert(asyncResult.Exception != null, "Task.IsFaulted guarantees non-null exception.");
                    ae = asyncResult.Exception;
                }
                else
                {
                    Fx.Assert(asyncResult.IsCompleted, "Task.Result is expected to be completed");

                    tuple = asyncResult.GetResult();
                    task = tuple.Item1 as Task;

                    if (task == null)
                    {
                        outputs = tuple.Item2;
                        return null;
                    }

                    if (task.IsFaulted)
                    {
                        Fx.Assert(task.Exception != null, "Task.IsFaulted guarantees non-null exception.");
                        ae = task.Exception;
                    }
                }

                if (ae != null && ae.InnerException != null)
                {
                    if (ae.InnerException is FaultException)
                    {
                        // If invokeTask.IsFaulted we produce the 'callFaulted' behavior below.
                        // Any other exception will retain 'callFailed' behavior.
                        //callFaulted = true;
                        //callFailed = false;
                    }

                    if (ae.InnerException is SecurityException)
                    {
                        DiagnosticUtility.TraceHandledException(ae.InnerException, TraceEventType.Warning);
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(AuthorizationBehavior.CreateAccessDeniedFaultException());
                    }

                    asyncResult.GetResult();
                }

                // Task cancellation without an exception indicates failure but we have no
                // additional information to provide.  Accessing Task.Result will throw a
                // TaskCanceledException.   For consistency between void Tasks and Task<T>,
                // we detect and throw here.
                if (task.IsCanceled)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TaskCanceledException(task));
                }

                outputs = tuple.Item2;

                returnVal = isGenericTask ? taskTResultGetMethod.Invoke(task, Type.EmptyTypes) : null;
                //callFailed = false;

                return returnVal;
            }
            finally
            {
                //if (boundOperation != null)
                //{
                //    ((IDisposable)boundOperation).Dispose();
                //}

                //ServiceModelActivity.Stop(activity);
                //AsyncMethodInvoker.StopOperationInvokeTrace(callFailed, callFaulted, TaskMethod.Name);
                //AsyncMethodInvoker.StopOperationInvokePerformanceCounters(callFailed, callFaulted, TaskMethod.Name);
            }
        }

        private async Task<Tuple<object, object[]>> InvokeAsync(object instance, object[] inputs)
        {
            EnsureIsInitialized();

            if (instance == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxNoServiceObject));
            }

            if (inputs == null)
            {
                if (inputParameterCount > 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInputParametersToServiceNull, inputParameterCount)));
                }
            }
            else if (inputs.Length != inputParameterCount)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInputParametersToServiceInvalid, inputParameterCount, inputs.Length)));
            }

            object[] outputs = EmptyArray<object>.Allocate(outputParameterCount);

            //AsyncMethodInvoker.StartOperationInvokePerformanceCounters(taskMethod.Name);

            object returnValue;
            bool callFailed = true;
            bool callFaulted = false;
            //ServiceModelActivity activity = null;
            //Activity boundActivity = null;

            try
            {
                //AsyncMethodInvoker.CreateActivityInfo(ref activity, ref boundActivity);
                //AsyncMethodInvoker.StartOperationInvokeTrace(taskMethod.Name);

                //if (DiagnosticUtility.ShouldUseActivity)
                //{
                //    string activityName = SR.Format(SR.ActivityExecuteMethod, taskMethod.DeclaringType.FullName, taskMethod.Name);
                //    ServiceModelActivity.Start(activity, activityName, ActivityType.ExecuteUserCode);
                //}

                returnValue = invokeDelegate(instance, inputs, outputs);

                if (returnValue == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("Task");
                }

                var returnValueTask = returnValue as Task;

                if (returnValueTask != null)
                {
                    // Only return once the task has completed                        
                    await returnValueTask;
                }

                callFailed = false;

                return Tuple.Create(returnValue, outputs);
            }
            catch (SecurityException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Warning);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(AuthorizationBehavior.CreateAccessDeniedFaultException());
            }
            catch (FaultException)
            {
                callFaulted = true;
                throw;
            }
            catch (Exception e)
            {
                TraceUtility.TraceUserCodeException(e, taskMethod);
                throw;
            }
            finally
            {
                //if (boundActivity != null)
                //{
                //    ((IDisposable)boundActivity).Dispose();
                //}

                //ServiceModelActivity.Stop(activity);

                // Any exception above means InvokeEnd will not be called, so complete it here.
                if (callFailed || callFaulted)
                {
                    //AsyncMethodInvoker.StopOperationInvokeTrace(callFailed, callFaulted, TaskMethod.Name);
                    //AsyncMethodInvoker.StopOperationInvokePerformanceCounters(callFailed, callFaulted, TaskMethod.Name);
                }
            }
        }

        // Helper method when implementing an APM wrapper around a Task based async method which returns a result. 
        // In the BeginMethod method, you would call use ToApm to wrap a call to MethodAsync:
        //     return MethodAsync(params).ToApm(callback, state);
        // In the EndMethod, you would use ToApmEnd<TResult> to ensure the correct exception handling
        // This will handle throwing exceptions in the correct place and ensure the IAsyncResult contains the provided
        // state object
        private static Task<TResult> ToApm<TResult>(Task<TResult> task, AsyncCallback callback, object state)
        {
            // When using APM, the returned IAsyncResult must have the passed in state object stored in AsyncState. This
            // is so the callback can regain state. If the incoming task already holds the state object, there's no need
            // to create a TaskCompletionSource to ensure the returned (IAsyncResult)Task has the right state object.
            // This is a performance optimization for this special case.
            if (task.AsyncState == state)
            {
                if (callback != null)
                {
                    task.ContinueWith((antecedent, obj) =>
                    {
                        AsyncCallback callbackObj = (AsyncCallback)obj;
                        callbackObj(antecedent);
                    }, callback, CancellationToken.None, TaskContinuationOptions.HideScheduler, TaskScheduler.Default);
                }

                return task;
            }

            // Need to create a TaskCompletionSource so that the returned Task object has the correct AsyncState value.
            var tcs = new TaskCompletionSource<TResult>(state);
            var continuationState = Tuple.Create(tcs, callback);

            task.ContinueWith((antecedent, obj) =>
            {
                Tuple<TaskCompletionSource<TResult>, AsyncCallback> tuple = (Tuple<TaskCompletionSource<TResult>, AsyncCallback>)obj;
                TaskCompletionSource<TResult> tcsObj = tuple.Item1;
                AsyncCallback callbackObj = tuple.Item2;

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

                if (callbackObj != null)
                {
                    callbackObj(tcsObj.Task);
                }
            }, continuationState, CancellationToken.None, TaskContinuationOptions.HideScheduler, TaskScheduler.Default);

            return tcs.Task;
        }

        private void EnsureIsInitialized()
        {
            if (this.invokeDelegate == null)
            {
                // Only pass locals byref because InvokerUtil may store temporary results in the byref.
                // If two threads both reference this.count, temporary results may interact.
                int inputParameterCount;
                int outputParameterCount;
                InvokeDelegate invokeDelegate = new InvokerUtil().GenerateInvokeDelegate(taskMethod, out inputParameterCount, out outputParameterCount);
                this.inputParameterCount = inputParameterCount;
                this.outputParameterCount = outputParameterCount;
                this.invokeDelegate = invokeDelegate;  // must set this last due to race
            }
        }
    }

}