// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Threading.Tasks;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class TaskMethodInvoker : IOperationInvoker
    {
        private const string ResultMethodName = "Result";
        private InvokeDelegate _invokeDelegate;
        private int _inputParameterCount;
        private int _outputParameterCount;
        private readonly MethodInfo _taskTResultGetMethod;
        private readonly bool _isGenericTask;

        public TaskMethodInvoker(MethodInfo taskMethod, Type taskType)
        {
            TaskMethod = taskMethod ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(taskMethod));

            if (taskType != ServiceReflector.VoidType)
            {
                _taskTResultGetMethod = ((PropertyInfo)taskMethod.ReturnType.GetMember(ResultMethodName)[0]).GetGetMethod();
                _isGenericTask = true;
            }
        }

        public MethodInfo TaskMethod { get; }

        public object[] AllocateInputs()
        {
            EnsureIsInitialized();

            return EmptyArray<object>.Allocate(_inputParameterCount);
        }

        public object Invoke(object instance, object[] inputs, out object[] outputs)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public IAsyncResult InvokeBegin(object instance, object[] inputs, AsyncCallback callback, object state)
        {
            return InvokeAsync(instance, inputs).ToApm(callback, state);
        }

        public async ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
        {
            if (instance == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxNoServiceObject));
            }
            //bool callFailed = true;
            //bool callFaulted = false;
            //ServiceModelActivity activity = null;
            //Activity boundOperation = null;

            try
            {
                //AsyncMethodInvoker.GetActivityInfo(ref activity, ref boundOperation);

                // This code would benefith from a rewrite to call TaskHelpers.ToApmEnd<Tuple<object, object[]>>
                // When doing so make sure there is enought test coverage se PR comment at link below for a good starting point
                // https://github.com/CoreWCF/CoreWCF/pull/54/files/8db6ff9ad6940a1056363defd1f6449adee56e1a#r333826132
                (Task returnValue, object[] outputs) tupleResult = await InvokeAsyncCore(instance, inputs);

                AggregateException ae = null;
                object[] outputs;
                if (!(tupleResult.returnValue is Task task))
                {
                    outputs = tupleResult.outputs;
                    return (null, outputs);
                }

                if (task.IsFaulted)
                {
                    Fx.Assert(task.Exception != null, "Task.IsFaulted guarantees non-null exception.");
                    ae = task.Exception;
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

                    // Rethrow inner exception as is
                    ExceptionDispatchInfo.Capture(ae.InnerException).Throw();
                }

                // Task cancellation without an exception indicates failure but we have no
                // additional information to provide.  Accessing Task.Result will throw a
                // TaskCanceledException.   For consistency between void Tasks and Task<T>,
                // we detect and throw here.
                if (task.IsCanceled)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TaskCanceledException(task));
                }

                outputs = tupleResult.outputs;

                object returnVal = _isGenericTask ? _taskTResultGetMethod.Invoke(task, Type.EmptyTypes) : null;
                //callFailed = false;

                return (returnVal, outputs);
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

        public ValueTask<(Task returnValue, object[] outputs)> InvokeAsyncCore(object instance, object[] inputs)
        {
            EnsureIsInitialized();

            if (instance == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxNoServiceObject));
            }

            if (inputs == null)
            {
                if (_inputParameterCount > 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInputParametersToServiceNull, _inputParameterCount)));
                }
            }
            else if (inputs.Length != _inputParameterCount)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInputParametersToServiceInvalid, _inputParameterCount, inputs.Length)));
            }

            object[] outputs = EmptyArray<object>.Allocate(_outputParameterCount);

            //AsyncMethodInvoker.StartOperationInvokePerformanceCounters(taskMethod.Name);

            object returnValue;
            //bool callFailed = true;
            //bool callFaulted = false;
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

                returnValue = _invokeDelegate(instance, inputs, outputs);

                if (returnValue == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(Task));
                }

                var returnValueTask = returnValue as Task;

                if (returnValueTask != null)
                {
                    // Return ValueTask which comletes once the task has completed
                    if (returnValueTask.IsCompleted)
                    {
                        if (returnValueTask.IsFaulted)
                        {
                            return new ValueTask<(Task returnValue, object[] outputs)>(Task.FromException<(Task returnValue, object[] outputs)>(ConvertExceptionForFaultedTask(returnValueTask)));
                        }
                        else
                        {
                            return new ValueTask<(Task returnValue, object[] outputs)>((returnValueTask, outputs));
                        }
                    }
                    else
                    {
                        Task<(Task returnValue, object[] outputs)> completionTask = returnValueTask.ContinueWith(antecedant =>
                        {
                            if (returnValueTask.IsFaulted)
                            {
                                throw ConvertExceptionForFaultedTask(antecedant);
                            }
                            else
                            {
                                return (returnValue: antecedant, outputs);
                            }
                        });

                        return new ValueTask<(Task returnValue, object[] outputs)>(completionTask);
                    }
                    //await returnValueTask;
                }

                // returnValue is null
                return new ValueTask<(Task returnValue, object[] outputs)>((returnValueTask, outputs));
            }
            finally
            {
                // TODO: When brining boundActivity back, make sure it executes in the correct order with relation to
                // called task completing.
                //if (boundActivity != null)
                //{
                //    ((IDisposable)boundActivity).Dispose();
                //}

                //ServiceModelActivity.Stop(activity);

                // Any exception above means InvokeEnd will not be called, so complete it here.
                //if (callFailed || callFaulted)
                //{
                //AsyncMethodInvoker.StopOperationInvokeTrace(callFailed, callFaulted, TaskMethod.Name);
                //AsyncMethodInvoker.StopOperationInvokePerformanceCounters(callFailed, callFaulted, TaskMethod.Name);
                //}
            }
        }

        private Exception ConvertExceptionForFaultedTask(Task task)
        {
            if (task.Exception?.InnerException is SecurityException innerException)
            {
                DiagnosticUtility.TraceHandledException(innerException, TraceEventType.Warning);
                return DiagnosticUtility.ExceptionUtility.ThrowHelperError(AuthorizationBehavior.CreateAccessDeniedFaultException());
            }

            TraceUtility.TraceUserCodeException(task.Exception, TaskMethod);

            return task.Exception;
        }

        private void EnsureIsInitialized()
        {
            if (_invokeDelegate == null)
            {
                // Only pass locals byref because InvokerUtil may store temporary results in the byref.
                // If two threads both reference this.count, temporary results may interact.
                InvokeDelegate invokeDelegate = InvokerUtil.GenerateInvokeDelegate(TaskMethod, out int inputParameterCount, out int outputParameterCount);
                _inputParameterCount = inputParameterCount;
                _outputParameterCount = outputParameterCount;
                _invokeDelegate = invokeDelegate;  // must set this last due to race
            }
        }
    }
}
