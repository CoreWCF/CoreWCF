// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class SyncMethodInvoker : IOperationInvoker
    {
        private readonly MethodInfo _method;
        private InvokeDelegate _invokeDelegate;
        private int _inputParameterCount;
        private int _outputParameterCount;
        private string _methodName;

        public SyncMethodInvoker(MethodInfo method)
        {
            if (method == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(method));
            }

            _method = method;
        }

        public MethodInfo Method
        {
            get
            {
                return _method;
            }
        }

        public string MethodName
        {
            get
            {
                if (_methodName == null)
                {
                    _methodName = _method.Name;
                }

                return _methodName;
            }
        }

        public object[] AllocateInputs()
        {
            EnsureIsInitialized();

            return EmptyArray<object>.Allocate(_inputParameterCount);
        }

        public object Invoke(object instance, object[] inputs, out object[] outputs)
        {
            throw new NotImplementedException();
        }

        public IAsyncResult InvokeBegin(object instance, object[] inputs, AsyncCallback callback, object state)
        {
            return InvokeAsync(instance, inputs).ToApm(callback, state);
        }

        public object InvokeEnd(object instance, out object[] outputs, IAsyncResult result)
        {
            var tuple = TaskHelpers.ToApmEnd<Tuple<object, object[]>>(result);
            outputs = tuple.Item2;
            return tuple.Item1;
        }

        public ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
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

            var outputs = EmptyArray<object>.Allocate(_outputParameterCount);

            //long beginOperation = 0;
            //bool callSucceeded = false;
            //bool callFaulted = false;

            //EventTraceActivity eventTraceActivity = null;
            //if (WcfEventSource.Instance.OperationCompletedIsEnabled() ||
            //        WcfEventSource.Instance.OperationFaultedIsEnabled() ||
            //        WcfEventSource.Instance.OperationFailedIsEnabled())
            //{
            //    beginOperation = DateTime.UtcNow.Ticks;
            //    OperationContext context = OperationContext.Current;
            //    if (context != null && context.IncomingMessage != null)
            //    {
            //        eventTraceActivity = EventTraceActivityHelper.TryExtractActivity(context.IncomingMessage);
            //    }
            //}

            object returnValue;
            try
            {
                //ServiceModelActivity activity = null;
                //IDisposable boundActivity = null;
                //if (DiagnosticUtility.ShouldUseActivity)
                //{
                //    activity = ServiceModelActivity.CreateBoundedActivity(true);
                //    boundActivity = activity;
                //}
                //else if (TraceUtility.MessageFlowTracingOnly)
                //{
                //    Guid activityId = TraceUtility.GetReceivedActivityId(OperationContext.Current);
                //    if (activityId != Guid.Empty)
                //    {
                //        DiagnosticTraceBase.ActivityId = activityId;
                //    }
                //}
                //else if (TraceUtility.ShouldPropagateActivity)
                //{
                //    //Message flow tracing only scenarios use a light-weight ActivityID management logic
                //    Guid activityId = ActivityIdHeader.ExtractActivityId(OperationContext.Current.IncomingMessage);
                //    if (activityId != Guid.Empty)
                //    {
                //        boundActivity = Activity.CreateActivity(activityId);
                //    }
                //}

                //using (boundActivity)
                //{
                //    if (DiagnosticUtility.ShouldUseActivity)
                //    {
                //        ServiceModelActivity.Start(activity, SR.Format(SR.ActivityExecuteMethod, _method.DeclaringType.FullName, _method.Name), ActivityType.ExecuteUserCode);
                //    }
                //    if (WcfEventSource.Instance.OperationInvokedIsEnabled())
                //    {
                //        WcfEventSource.Instance.OperationInvoked(eventTraceActivity, MethodName, TraceUtility.GetCallerInfo(OperationContext.Current));
                //    }
                returnValue = _invokeDelegate(instance, inputs, outputs);
                //callSucceeded = true;
                //}
            }
            catch (FaultException)
            {
                //callFaulted = true;
                throw;
            }
            finally
            {
                //if (beginOperation != 0)
                //{
                //    if (callSucceeded)
                //    {
                //        if (WcfEventSource.Instance.OperationCompletedIsEnabled())
                //        {
                //            WcfEventSource.Instance.OperationCompleted(eventTraceActivity, _methodName,
                //                TraceUtility.GetUtcBasedDurationForTrace(beginOperation));
                //        }
                //    }
                //    else if (callFaulted)
                //    {
                //        if (WcfEventSource.Instance.OperationFaultedIsEnabled())
                //        {
                //            WcfEventSource.Instance.OperationFaulted(eventTraceActivity, _methodName,
                //                TraceUtility.GetUtcBasedDurationForTrace(beginOperation));
                //        }
                //    }
                //    else
                //    {
                //        if (WcfEventSource.Instance.OperationFailedIsEnabled())
                //        {
                //            WcfEventSource.Instance.OperationFailed(eventTraceActivity, _methodName,
                //                TraceUtility.GetUtcBasedDurationForTrace(beginOperation));
                //        }
                //    }
                //}
            }

            return new ValueTask<(object returnValue, object[] outputs)>((returnValue, outputs));
        }

        private void EnsureIsInitialized()
        {
            if (_invokeDelegate == null)
            {
                EnsureIsInitializedCore();
            }
        }

        private void EnsureIsInitializedCore()
        {
            // Only pass locals byref because InvokerUtil may store temporary results in the byref.
            // If two threads both reference this.count, temporary results may interact.
            var invokeDelegate = new InvokerUtil().GenerateInvokeDelegate(Method, out int inputParameterCount, out int outputParameterCount);
            _outputParameterCount = outputParameterCount;
            _inputParameterCount = inputParameterCount;
            _invokeDelegate = invokeDelegate;  // must set this last due to race
        }
    }
}