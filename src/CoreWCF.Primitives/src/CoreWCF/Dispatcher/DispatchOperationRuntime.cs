using System;
using System.Globalization;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using CoreWCF.Security;

namespace CoreWCF.Dispatcher
{
    internal class DispatchOperationRuntime
    {
        readonly string action;
        readonly ICallContextInitializer[] callContextInitializers;
        readonly IDispatchFaultFormatter faultFormatter;
        readonly IDispatchMessageFormatter formatter;
        readonly ImpersonationOption impersonation;
        readonly IParameterInspector[] inspectors;
        readonly IOperationInvoker invoker;
        readonly bool isTerminating;
        readonly bool isSessionOpenNotificationEnabled;
        readonly bool isSynchronous;
        readonly string name;
        readonly ImmutableDispatchRuntime parent;
        readonly bool releaseInstanceAfterCall;
        readonly bool releaseInstanceBeforeCall;
        readonly string replyAction;
        //readonly bool transactionAutoComplete;
        //readonly bool transactionRequired;
        readonly bool deserializeRequest;
        readonly bool serializeReply;
        readonly bool isOneWay;
        readonly bool disposeParameters;
        readonly ReceiveContextAcknowledgementMode receiveContextAcknowledgementMode;
        readonly bool bufferedReceiveEnabled;
        //readonly bool isInsideTransactedReceiveScope;

        internal DispatchOperationRuntime(DispatchOperation operation, ImmutableDispatchRuntime parent)
        {
            if (operation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(operation));
            }
            if (parent == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parent));
            }
            if (operation.Invoker == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.RuntimeRequiresInvoker0));
            }

            disposeParameters = ((operation.AutoDisposeParameters) && (!operation.HasNoDisposableParameters));
            this.parent = parent;
            callContextInitializers = EmptyArray<ICallContextInitializer>.ToArray(operation.CallContextInitializers);
            inspectors = EmptyArray<IParameterInspector>.ToArray(operation.ParameterInspectors);
            faultFormatter = operation.FaultFormatter;
            impersonation = operation.Impersonation;
            deserializeRequest = operation.DeserializeRequest;
            serializeReply = operation.SerializeReply;
            formatter = operation.Formatter;
            invoker = operation.Invoker;

            try
            {
                isSynchronous = operation.Invoker.IsSynchronous;
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
            }
            isTerminating = operation.IsTerminating;
            isSessionOpenNotificationEnabled = operation.IsSessionOpenNotificationEnabled;
            action = operation.Action;
            name = operation.Name;
            releaseInstanceAfterCall = operation.ReleaseInstanceAfterCall;
            releaseInstanceBeforeCall = operation.ReleaseInstanceBeforeCall;
            replyAction = operation.ReplyAction;
            isOneWay = operation.IsOneWay;
            receiveContextAcknowledgementMode = operation.ReceiveContextAcknowledgementMode;
            bufferedReceiveEnabled = operation.BufferedReceiveEnabled;

            if (formatter == null && (deserializeRequest || serializeReply))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.DispatchRuntimeRequiresFormatter0, name)));
            }

            if ((operation.Parent.InstanceProvider == null) && (operation.Parent.Type != null))
            {
                SyncMethodInvoker sync = invoker as SyncMethodInvoker;
                if (sync != null)
                {
                    ValidateInstanceType(operation.Parent.Type, sync.Method);
                }

                //AsyncMethodInvoker async = this.invoker as AsyncMethodInvoker;
                //if (async != null)
                //{
                //    this.ValidateInstanceType(operation.Parent.Type, async.BeginMethod);
                //    this.ValidateInstanceType(operation.Parent.Type, async.EndMethod);
                //}

                TaskMethodInvoker task = invoker as TaskMethodInvoker;
                if (task != null)
                {
                    ValidateInstanceType(operation.Parent.Type, task.TaskMethod);
                }
            }
        }

        internal string Action
        {
            get { return action; }
        }

        internal ICallContextInitializer[] CallContextInitializers
        {
            get { return callContextInitializers; }
        }

        internal bool DisposeParameters
        {
            get { return disposeParameters; }
        }

        internal bool HasDefaultUnhandledActionInvoker
        {
            get { return (invoker is DispatchRuntime.UnhandledActionInvoker); }
        }

        internal bool SerializeReply
        {
            get { return serializeReply; }
        }

        internal IDispatchFaultFormatter FaultFormatter
        {
            get { return faultFormatter; }
        }

        internal IDispatchMessageFormatter Formatter
        {
            get { return formatter; }
        }

        internal ImpersonationOption Impersonation
        {
            get { return impersonation; }
        }

        internal IOperationInvoker Invoker
        {
            get { return invoker; }
        }

        internal bool IsSynchronous
        {
            get { return isSynchronous; }
        }

        internal bool IsOneWay
        {
            get { return isOneWay; }
        }

        internal bool IsTerminating
        {
            get { return isTerminating; }
        }

        internal string Name
        {
            get { return name; }
        }

        internal IParameterInspector[] ParameterInspectors
        {
            get { return inspectors; }
        }

        internal ImmutableDispatchRuntime Parent
        {
            get { return parent; }
        }

        internal ReceiveContextAcknowledgementMode ReceiveContextAcknowledgementMode
        {
            get { return receiveContextAcknowledgementMode; }
        }

        internal bool ReleaseInstanceAfterCall
        {
            get { return releaseInstanceAfterCall; }
        }

        internal bool ReleaseInstanceBeforeCall
        {
            get { return releaseInstanceBeforeCall; }
        }

        internal string ReplyAction
        {
            get { return replyAction; }
        }

        //internal bool TransactionAutoComplete
        //{
        //    get { return this.transactionAutoComplete; }
        //}

        //internal bool TransactionRequired
        //{
        //    get { return this.transactionRequired; }
        //}

        //internal bool IsInsideTransactedReceiveScope
        //{
        //    get { return this.isInsideTransactedReceiveScope; }
        //}

        void DeserializeInputs(MessageRpc rpc)
        {
            //bool success = false;
            try
            {
                try
                {
                    rpc.InputParameters = Invoker.AllocateInputs();
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    if (ErrorBehavior.ShouldRethrowExceptionAsIs(e))
                    {
                        throw;
                    }
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
                }
                try
                {
                    // If the field is true, then this operation is to be invoked at the time the service 
                    // channel is opened. The incoming message is created at ChannelHandler level with no 
                    // content, so we don't need to deserialize the message.
                    if (!isSessionOpenNotificationEnabled)
                    {
                        if (deserializeRequest)
                        {
                            //if (TD.DispatchFormatterDeserializeRequestStartIsEnabled())
                            //{
                            //    TD.DispatchFormatterDeserializeRequestStart(rpc.EventTraceActivity);
                            //}

                            Formatter.DeserializeRequest(rpc.Request, rpc.InputParameters);

                            //if (TD.DispatchFormatterDeserializeRequestStopIsEnabled())
                            //{
                            //    TD.DispatchFormatterDeserializeRequestStop(rpc.EventTraceActivity);
                            //}
                        }
                        else
                        {
                            rpc.InputParameters[0] = rpc.Request;
                        }
                    }

                    //success = true;
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    if (ErrorBehavior.ShouldRethrowExceptionAsIs(e))
                    {
                        throw;
                    }
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
                }
            }
            finally
            {
                rpc.DidDeserializeRequestBody = (rpc.Request.State != MessageState.Created);

                //if (!success && MessageLogger.LoggingEnabled)
                //{
                //    MessageLogger.LogMessage(rpc.Request, MessageLoggingSource.Malformed);
                //}
            }
        }

        void InitializeCallContext(MessageRpc rpc)
        {
            if (CallContextInitializers.Length > 0)
            {
                InitializeCallContextCore(rpc);
            }
        }

        void InitializeCallContextCore(MessageRpc rpc)
        {
            IClientChannel channel = rpc.Channel.Proxy as IClientChannel;
            int offset = Parent.CallContextCorrelationOffset;

            try
            {
                for (int i = 0; i < rpc.Operation.CallContextInitializers.Length; i++)
                {
                    ICallContextInitializer initializer = CallContextInitializers[i];
                    rpc.Correlation[offset + i] = initializer.BeforeInvoke(rpc.InstanceContext, channel, rpc.Request);
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                if (ErrorBehavior.ShouldRethrowExceptionAsIs(e))
                {
                    throw;
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
            }
        }

        void UninitializeCallContext(MessageRpc rpc)
        {
            if (CallContextInitializers.Length > 0)
            {
                UninitializeCallContextCore(rpc);
            }
        }

        void UninitializeCallContextCore(MessageRpc rpc)
        {
            IClientChannel channel = rpc.Channel.Proxy as IClientChannel;
            int offset = Parent.CallContextCorrelationOffset;

            try
            {
                for (int i = CallContextInitializers.Length - 1; i >= 0; i--)
                {
                    ICallContextInitializer initializer = CallContextInitializers[i];
                    initializer.AfterInvoke(rpc.Correlation[offset + i]);
                }
            }
            catch (Exception e)
            {
                // thread-local storage may be corrupt
                DiagnosticUtility.FailFast(string.Format(CultureInfo.InvariantCulture, "ICallContextInitializer.BeforeInvoke threw an exception of type {0}: {1}", e.GetType(), e.Message));
            }
        }

        void InspectInputs(MessageRpc rpc)
        {
            if (ParameterInspectors.Length > 0)
            {
                InspectInputsCore(rpc);
            }
        }

        void InspectInputsCore(MessageRpc rpc)
        {
            int offset = Parent.ParameterInspectorCorrelationOffset;

            for (int i = 0; i < ParameterInspectors.Length; i++)
            {
                IParameterInspector inspector = ParameterInspectors[i];
                rpc.Correlation[offset + i] = inspector.BeforeCall(Name, rpc.InputParameters);
                //if (TD.ParameterInspectorBeforeCallInvokedIsEnabled())
                //{
                //    TD.ParameterInspectorBeforeCallInvoked(rpc.EventTraceActivity, this.ParameterInspectors[i].GetType().FullName);
                //}
            }
        }

        void InspectOutputs(MessageRpc rpc)
        {
            if (ParameterInspectors.Length > 0)
            {
                InspectOutputsCore(rpc);
            }
        }

        void InspectOutputsCore(MessageRpc rpc)
        {
            int offset = Parent.ParameterInspectorCorrelationOffset;

            for (int i = ParameterInspectors.Length - 1; i >= 0; i--)
            {
                IParameterInspector inspector = ParameterInspectors[i];
                inspector.AfterCall(Name, rpc.OutputParameters, rpc.ReturnParameter, rpc.Correlation[offset + i]);
                //if (TD.ParameterInspectorAfterCallInvokedIsEnabled())
                //{
                //    TD.ParameterInspectorAfterCallInvoked(rpc.EventTraceActivity, this.ParameterInspectors[i].GetType().FullName);
                //}
            }
        }

        internal async Task<MessageRpc> InvokeAsync(MessageRpc rpc)
        {
            if (rpc.Error == null)
            {
                try
                {
                    InitializeCallContext(rpc);
                    object target = rpc.Instance;
                    DeserializeInputs(rpc);
                    InspectInputs(rpc);
                    ValidateMustUnderstand(rpc);

                    if (parent.RequireClaimsPrincipalOnOperationContext)
                    {
                        SetClaimsPrincipalToOperationContext(rpc);
                    }

                    if (parent.SecurityImpersonation?.IsSecurityContextImpersonationRequired(rpc) ?? false)
                    {
                        await parent.SecurityImpersonation.RunImpersonated(rpc, async () =>
                        {
                            if (isSynchronous)
                            {
                                rpc.ReturnParameter = Invoker.Invoke(target, rpc.InputParameters, out rpc.OutputParameters);
                            }
                            else
                            {
                                (rpc.ReturnParameter, rpc.OutputParameters) = await Invoker.InvokeAsync(target, rpc.InputParameters);
                            }
                        });
                    }
                    else
                    {
                        if (isSynchronous)
                        {
                            rpc.ReturnParameter = Invoker.Invoke(target, rpc.InputParameters, out rpc.OutputParameters);
                        }
                        else
                        {
                            (rpc.ReturnParameter, rpc.OutputParameters) = await Invoker.InvokeAsync(target, rpc.InputParameters);
                        }
                    }

                    InspectOutputs(rpc);
                    SerializeOutputs(rpc);
                }
                catch { throw; } // Make sure user Exception filters are not impersonated.
                finally
                {
                    UninitializeCallContext(rpc);
                }
            }

            return rpc;
        }

        void SetClaimsPrincipalToOperationContext(MessageRpc rpc)
        {
            // TODO: Reenable this code

            //ServiceSecurityContext securityContext = rpc.SecurityContext;
            //if (!rpc.HasSecurityContext)
            //{
            //    SecurityMessageProperty securityContextProperty = rpc.Request.Properties.Security;
            //    if (securityContextProperty != null)
            //    {
            //        securityContext = securityContextProperty.ServiceSecurityContext;
            //    }
            //}

            //if (securityContext != null)
            //{
            //    object principal;
            //    if (securityContext.AuthorizationContext.Properties.TryGetValue(AuthorizationPolicy.ClaimsPrincipalKey, out principal))
            //    {
            //        ClaimsPrincipal claimsPrincipal = principal as ClaimsPrincipal;
            //        if (claimsPrincipal != null)
            //        {
            //            //
            //            // Always set ClaimsPrincipal to OperationContext.Current if identityModel pipeline is used.
            //            //
            //            OperationContext.Current.ClaimsPrincipal = claimsPrincipal;
            //        }
            //        else
            //        {
            //            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.NoPrincipalSpecifiedInAuthorizationContext));
            //        }
            //    }
            //}
        }

        void SerializeOutputs(MessageRpc rpc)
        {
            if (!IsOneWay && parent.EnableFaults)
            {
                Message reply;
                if (serializeReply)
                {
                    try
                    {
                        //if (TD.DispatchFormatterSerializeReplyStartIsEnabled())
                        //{
                        //    TD.DispatchFormatterSerializeReplyStart(rpc.EventTraceActivity);
                        //}

                        reply = Formatter.SerializeReply(rpc.RequestVersion, rpc.OutputParameters, rpc.ReturnParameter);

                        //if (TD.DispatchFormatterSerializeReplyStopIsEnabled())
                        //{
                        //    TD.DispatchFormatterSerializeReplyStop(rpc.EventTraceActivity);
                        //}
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        if (ErrorBehavior.ShouldRethrowExceptionAsIs(e))
                        {
                            throw;
                        }
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
                    }

                    if (reply == null)
                    {
                        string message = SR.Format(SR.SFxNullReplyFromFormatter2, Formatter.GetType().ToString(), (name ?? ""));
                        ErrorBehavior.ThrowAndCatch(new InvalidOperationException(message));
                    }
                }
                else
                {
                    if ((rpc.ReturnParameter == null) && (rpc.OperationContext.RequestContext != null))
                    {
                        string message = SR.Format(SR.SFxDispatchRuntimeMessageCannotBeNull, name);
                        ErrorBehavior.ThrowAndCatch(new InvalidOperationException(message));
                    }

                    reply = (Message)rpc.ReturnParameter;

                    if ((reply != null) && (!ProxyOperationRuntime.IsValidAction(reply, ReplyAction)))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxInvalidReplyAction, Name, reply.Headers.Action ?? "{NULL}", ReplyAction)));
                    }
                }

                //if (DiagnosticUtility.ShouldUseActivity && rpc.Activity != null && reply != null)
                //{
                //    TraceUtility.SetActivity(reply, rpc.Activity);
                //    if (TraceUtility.ShouldPropagateActivity)
                //    {
                //        TraceUtility.AddActivityHeader(reply);
                //    }
                //}
                //else if (TraceUtility.ShouldPropagateActivity && reply != null && rpc.ResponseActivityId != Guid.Empty)
                //{
                //    ActivityIdHeader header = new ActivityIdHeader(rpc.ResponseActivityId);
                //    header.AddTo(reply);
                //}

                //rely on the property set during the message receive to correlate the trace
                //if (TraceUtility.MessageFlowTracingOnly)
                //{
                //    //Guard against MEX scenarios where the message is closed by now
                //    if (null != rpc.OperationContext.IncomingMessage && MessageState.Closed != rpc.OperationContext.IncomingMessage.State)
                //    {
                //        FxTrace.Trace.SetAndTraceTransfer(TraceUtility.GetReceivedActivityId(rpc.OperationContext), true);
                //    }
                //    else
                //    {
                //        if (rpc.ResponseActivityId != Guid.Empty)
                //        {
                //            FxTrace.Trace.SetAndTraceTransfer(rpc.ResponseActivityId, true);
                //        }
                //    }
                //}

                // Add the ImpersonateOnSerializingReplyMessageProperty on the reply message iff
                // a. reply message is not null.
                // b. Impersonation is enabled on serializing Reply

                if (reply != null && this.parent.IsImpersonationEnabledOnSerializingReply)
                {
                    bool shouldImpersonate = this.parent.SecurityImpersonation != null && this.parent.SecurityImpersonation.IsImpersonationEnabledOnCurrentOperation(rpc);
                    if (shouldImpersonate)
                    {
                        reply.Properties.Add(ImpersonateOnSerializingReplyMessageProperty.Name, new ImpersonateOnSerializingReplyMessageProperty(rpc));
                        reply = new ImpersonatingMessage(reply);
                    }
                }

                //if (MessageLogger.LoggingEnabled && null != reply)
                //{
                //    MessageLogger.LogMessage(ref reply, MessageLoggingSource.ServiceLevelSendReply | MessageLoggingSource.LastChance);
                //}
                rpc.Reply = reply;
            }
        }

        void ValidateInstanceType(Type type, MethodInfo method)
        {
            if (!method.DeclaringType.IsAssignableFrom(type))
            {
                string message = SR.Format(SR.SFxMethodNotSupportedByType2,type.FullName,
                                              method.DeclaringType.FullName);

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(message));
            }
        }

        void ValidateMustUnderstand(MessageRpc rpc)
        {
            if (parent.ValidateMustUnderstand)
            {
                rpc.NotUnderstoodHeaders = rpc.Request.Headers.GetHeadersNotUnderstood();
                if (rpc.NotUnderstoodHeaders != null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new MustUnderstandSoapException(rpc.NotUnderstoodHeaders, rpc.Request.Version.Envelope));
                }
            }
        }
    }

}