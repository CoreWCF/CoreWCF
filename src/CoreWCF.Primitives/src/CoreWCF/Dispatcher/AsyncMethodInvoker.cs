//using System;
//using System.Reflection;
//using CoreWCF.Description;
//using CoreWCF.Diagnostics;

//namespace CoreWCF.Dispatcher
//{
//    public class AsyncMethodInvoker : IOperationInvoker
//    {
//        MethodInfo beginMethod;
//        MethodInfo endMethod;
//        InvokeBeginDelegate invokeBeginDelegate;
//        InvokeEndDelegate invokeEndDelegate;
//        int inputParameterCount;
//        int outputParameterCount;

//        public bool IsSynchronous => false;

//        public object[] AllocateInputs()
//        {
//            return EmptyArray<object>.Allocate(this.InputParameterCount);
//        }

//        public object Invoke(object instance, object[] inputs, out object[] outputs)
//        {
//            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
//        }

//        public IAsyncResult InvokeBegin(object instance, object[] inputs, AsyncCallback callback, object state)
//        {
//            if (instance == null)
//                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxNoServiceObject));
//            if (inputs == null)
//            {
//                if (this.InputParameterCount > 0)
//                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInputParametersToServiceNull, this.InputParameterCount)));
//            }
//            else if (inputs.Length != this.InputParameterCount)
//                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInputParametersToServiceInvalid, this.InputParameterCount, inputs.Length)));

//            //StartOperationInvokePerformanceCounters(this.beginMethod.Name.Substring(ServiceReflector.BeginMethodNamePrefix.Length));

//            IAsyncResult returnValue;
//            //bool callFailed = true;
//            //bool callFaulted = false;
//            //ServiceModelActivity activity = null;
//            try
//            {
//                //Activity boundActivity = null;
//                //CreateActivityInfo(ref activity, ref boundActivity);

//                //StartOperationInvokeTrace(this.beginMethod.Name);

//                //using (boundActivity)
//                //{
//                //    if (DiagnosticUtility.ShouldUseActivity)
//                //    {
//                //        string activityName = null;

//                //        if (this.endMethod == null)
//                //        {
//                //            activityName = SR.Format(SR.ActivityExecuteMethod,
//                //                this.beginMethod.DeclaringType.FullName, this.beginMethod.Name);
//                //        }
//                //        else
//                //        {
//                //            activityName = SR.Format(SR.ActivityExecuteAsyncMethod,
//                //                this.beginMethod.DeclaringType.FullName, this.beginMethod.Name,
//                //                this.endMethod.DeclaringType.FullName, this.endMethod.Name);
//                //        }

//                //        ServiceModelActivity.Start(activity, activityName, ActivityType.ExecuteUserCode);
//                //    }

//                    returnValue = this.InvokeBeginDelegate(instance, inputs, callback, state);
//                    //callFailed = false;
//                //}
//            }
//            catch (System.Security.SecurityException e)
//            {
//                DiagnosticUtility.TraceHandledException(e, TraceEventType.Warning);
//                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(AuthorizationBehavior.CreateAccessDeniedFaultException());
//            }
//            catch (Exception e)
//            {
//                TraceUtility.TraceUserCodeException(e, this.beginMethod);
//                //if (e is FaultException)
//                //{
//                //    callFaulted = true;
//                //    callFailed = false;
//                //}

//                throw;
//            }
//            finally
//            {
//                //ServiceModelActivity.Stop(activity);

//                //// An exception during the InvokeBegin will not call InvokeEnd,
//                //// so we complete the trace and performance counters here.
//                //if (callFailed || callFaulted)
//                //{
//                //    StopOperationInvokeTrace(callFailed, callFaulted, this.EndMethod.Name);
//                //    StopOperationInvokePerformanceCounters(callFailed, callFaulted, endMethod.Name.Substring(ServiceReflector.EndMethodNamePrefix.Length));
//                //}
//            }
//            return returnValue;

//        }

//        public object InvokeEnd(object instance, out object[] outputs, IAsyncResult result)
//        {
//            throw new NotImplementedException();
//        }

//        InvokeBeginDelegate InvokeBeginDelegate
//        {
//            get
//            {
//                EnsureIsInitialized();
//                return invokeBeginDelegate;
//            }
//        }

//        InvokeEndDelegate InvokeEndDelegate
//        {
//            get
//            {
//                EnsureIsInitialized();
//                return invokeEndDelegate;
//            }
//        }

//        int InputParameterCount
//        {
//            get
//            {
//                EnsureIsInitialized();
//                return this.inputParameterCount;
//            }
//        }

//        void EnsureIsInitialized()
//        {
//            if (this.invokeBeginDelegate == null)
//            {
//                // Only pass locals byref because InvokerUtil may store temporary results in the byref.
//                // If two threads both reference this.count, temporary results may interact.
//                int inputParameterCount;
//                InvokeBeginDelegate invokeBeginDelegate = new InvokerUtil().GenerateInvokeBeginDelegate(this.beginMethod, out inputParameterCount);
//                this.inputParameterCount = inputParameterCount;

//                int outputParameterCount;
//                InvokeEndDelegate invokeEndDelegate = new InvokerUtil().GenerateInvokeEndDelegate(this.endMethod, out outputParameterCount);
//                this.outputParameterCount = outputParameterCount;
//                this.invokeEndDelegate = invokeEndDelegate;
//                this.invokeBeginDelegate = invokeBeginDelegate;  // must set this last due to race
//            }
//        }
//    }
//}