// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Claims;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.AspNetCore.Authorization;
using Claim = CoreWCF.IdentityModel.Claims.Claim;

namespace CoreWCF.Dispatcher
{
    internal class DispatchOperationRuntime
    {
        private readonly bool _isSessionOpenNotificationEnabled;

        //readonly bool transactionAutoComplete;
        //readonly bool transactionRequired;
        private readonly bool _deserializeRequest;

        //readonly bool isInsideTransactedReceiveScope;

        internal DispatchOperationRuntime(DispatchOperation operation, ImmutableDispatchRuntime parent)
        {
            if (operation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(operation));
            }

            if (operation.Invoker == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.RuntimeRequiresInvoker0));
            }

            DisposeParameters = ((operation.AutoDisposeParameters) && (!operation.HasNoDisposableParameters));
            Parent = parent ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parent));
            CallContextInitializers = EmptyArray<ICallContextInitializer>.ToArray(operation.CallContextInitializers);
            ParameterInspectors = EmptyArray<IParameterInspector>.ToArray(operation.ParameterInspectors);
            FaultFormatter = operation.FaultFormatter;
            Impersonation = operation.Impersonation;
            AuthorizeClaims = operation.AuthorizeClaims;
            AuthorizationPolicy = operation.AuthorizationPolicy;
            _deserializeRequest = operation.DeserializeRequest;
            SerializeReply = operation.SerializeReply;
            Formatter = operation.Formatter;
            Invoker = operation.Invoker;
            IsTerminating = operation.IsTerminating;
            _isSessionOpenNotificationEnabled = operation.IsSessionOpenNotificationEnabled;
            Action = operation.Action;
            Name = operation.Name;
            ReleaseInstanceAfterCall = operation.ReleaseInstanceAfterCall;
            ReleaseInstanceBeforeCall = operation.ReleaseInstanceBeforeCall;
            ReplyAction = operation.ReplyAction;
            IsOneWay = operation.IsOneWay;
            ReceiveContextAcknowledgementMode = operation.ReceiveContextAcknowledgementMode;

            if (Formatter == null && (_deserializeRequest || SerializeReply))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.DispatchRuntimeRequiresFormatter0, Name)));
            }

            if ((operation.Parent.InstanceProvider == null) && (operation.Parent.Type != null))
            {
                if (Invoker is SyncMethodInvoker sync)
                {
                    ValidateInstanceType(operation.Parent.Type, sync.Method);
                }

                //AsyncMethodInvoker async = this.invoker as AsyncMethodInvoker;
                //if (async != null)
                //{
                //    this.ValidateInstanceType(operation.Parent.Type, async.BeginMethod);
                //    this.ValidateInstanceType(operation.Parent.Type, async.EndMethod);
                //}

                if (Invoker is TaskMethodInvoker task)
                {
                    ValidateInstanceType(operation.Parent.Type, task.TaskMethod);
                }
            }
        }

        internal string Action { get; }

        internal ICallContextInitializer[] CallContextInitializers { get; }

        internal bool DisposeParameters { get; }

        internal bool HasDefaultUnhandledActionInvoker
        {
            get { return (Invoker is DispatchRuntime.UnhandledActionInvoker); }
        }

        internal bool SerializeReply { get; }

        internal IDispatchFaultFormatter FaultFormatter { get; }

        internal IDispatchMessageFormatter Formatter { get; }

        internal ImpersonationOption Impersonation { get; }

        internal ConcurrentDictionary<string, List<Claim>> AuthorizeClaims { get; }

        internal Lazy<AuthorizationPolicy> AuthorizationPolicy { get; }

        internal IOperationInvoker Invoker { get; }

        internal bool IsOneWay { get; }

        internal bool IsTerminating { get; }

        internal string Name { get; }

        internal IParameterInspector[] ParameterInspectors { get; }

        internal ImmutableDispatchRuntime Parent { get; }

        internal ReceiveContextAcknowledgementMode ReceiveContextAcknowledgementMode { get; }

        internal bool ReleaseInstanceAfterCall { get; }

        internal bool ReleaseInstanceBeforeCall { get; }

        internal string ReplyAction { get; }

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

        private void DeserializeInputs(MessageRpc rpc)
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
                    if (!_isSessionOpenNotificationEnabled)
                    {
                        if (_deserializeRequest)
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

        private void InitializeCallContext(MessageRpc rpc)
        {
            if (CallContextInitializers.Length > 0)
            {
                InitializeCallContextCore(rpc);
            }
        }

        private void InitializeCallContextCore(MessageRpc rpc)
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

        private void UninitializeCallContext(MessageRpc rpc)
        {
            if (CallContextInitializers.Length > 0)
            {
                UninitializeCallContextCore(rpc);
            }
        }

        private void UninitializeCallContextCore(MessageRpc rpc)
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

        private void InspectInputs(MessageRpc rpc)
        {
            if (ParameterInspectors.Length > 0)
            {
                InspectInputsCore(rpc);
            }
        }

        private void InspectInputsCore(MessageRpc rpc)
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

        private void InspectOutputs(MessageRpc rpc)
        {
            if (ParameterInspectors.Length > 0)
            {
                InspectOutputsCore(rpc);
            }
        }

        private void InspectOutputsCore(MessageRpc rpc)
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
                    ValidateAuthorizedClaims(rpc);

                    if (Parent.RequireClaimsPrincipalOnOperationContext)
                    {
                        SetClaimsPrincipalToOperationContext(rpc);
                    }

                    if (Parent.SecurityImpersonation != null)
                    {
                        await Parent.SecurityImpersonation.RunImpersonated(rpc, async () =>
                        {
                            (rpc.ReturnParameter, rpc.OutputParameters) = await Invoker.InvokeAsync(target, rpc.InputParameters);
                        });
                    }
                    else
                    {
                        (rpc.ReturnParameter, rpc.OutputParameters) = await Invoker.InvokeAsync(target, rpc.InputParameters);
                    }

                    InspectOutputs(rpc);
                    SerializeOutputs(rpc);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
                {
                    ExceptionDispatchInfo.Capture(ex).Throw();
                } // Make sure user Exception filters are not impersonated.
#pragma warning restore CA1031 // Do not catch general exception types
                finally
                {
                    UninitializeCallContext(rpc);
                }
            }

            return rpc;
        }

        internal void SetClaimsPrincipalToOperationContext(MessageRpc rpc)
        {
            ServiceSecurityContext securityContext = rpc.SecurityContext;
            if (!rpc.HasSecurityContext)
            {
                SecurityMessageProperty securityContextProperty = rpc.Request.Properties.Security;
                if (securityContextProperty != null)
                {
                    securityContext = securityContextProperty.ServiceSecurityContext;
                }
            }

            if (securityContext != null)
            {
                object principal;
                if (securityContext.AuthorizationContext.Properties.TryGetValue(IdentityModel.Tokens.AuthorizationPolicy.ClaimsPrincipalKey, out principal))
                {
                    ClaimsPrincipal claimsPrincipal = principal as ClaimsPrincipal;
                    if (claimsPrincipal != null)
                    {
                        //
                        // Always set ClaimsPrincipal to OperationContext.Current if identityModel pipeline is used.
                        //
                        OperationContext.Current.ClaimsPrincipal = claimsPrincipal;
                    }
                    else
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.NoPrincipalSpecifiedInAuthorizationContext));
                    }
                }
            }
        }

        private void SerializeOutputs(MessageRpc rpc)
        {
            if (!IsOneWay && Parent.EnableFaults)
            {
                Message reply;
                if (SerializeReply)
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
                        string message = SR.Format(SR.SFxNullReplyFromFormatter2, Formatter.GetType().ToString(), (Name ?? ""));
                        ErrorBehavior.ThrowAndCatch(new InvalidOperationException(message));
                    }
                }
                else
                {
                    if ((rpc.ReturnParameter == null) && (rpc.OperationContext.RequestContext != null))
                    {
                        string message = SR.Format(SR.SFxDispatchRuntimeMessageCannotBeNull, Name);
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
                // b. Impersonation is enabled on serializing ReplyAsync

                if (reply != null && Parent.IsImpersonationEnabledOnSerializingReply)
                {
                    bool shouldImpersonate = Parent.SecurityImpersonation != null && Parent.SecurityImpersonation.IsImpersonationEnabledOnCurrentOperation(rpc);
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

        private void ValidateInstanceType(Type type, MethodInfo method)
        {
            if (!method.DeclaringType.IsAssignableFrom(type))
            {
                string message = SR.Format(SR.SFxMethodNotSupportedByType2, type.FullName,
                                              method.DeclaringType.FullName);

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(message));
            }
        }

        private void ValidateMustUnderstand(MessageRpc rpc)
        {
            if (Parent.ValidateMustUnderstand)
            {
                rpc.NotUnderstoodHeaders = rpc.Request.Headers.GetHeadersNotUnderstood();
                if (rpc.NotUnderstoodHeaders != null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new MustUnderstandSoapException(rpc.NotUnderstoodHeaders, rpc.Request.Version.Envelope));
                }
            }
        }

        private void ValidateAuthorizedClaims(MessageRpc rpc)
        {
            if (AuthorizeClaims.IsEmpty || AuthorizeClaims.Keys.Count == 0)
                return;
            if (rpc.OperationContext?.ServiceSecurityContext.AuthorizationPolicies == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(AuthorizationBehavior.CreateAccessDeniedFaultException());
            }

            foreach (var eachAuthClaim in AuthorizeClaims)
            {
                List<Claim> allClaims = eachAuthClaim.Value;
                if(!IsClaimFound(rpc.OperationContext?.ServiceSecurityContext.AuthorizationPolicies, allClaims))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(AuthorizationBehavior.CreateAccessDeniedFaultException());
                }
            }
        }

        private bool IsClaimFound(ReadOnlyCollection<IAuthorizationPolicy> policies, List<Claim> anyClaims)
        {
            foreach (var policy in policies)
            {
                if(policy is UnconditionalPolicy)
                {
                    var claimSets = ((UnconditionalPolicy)policy).Issuances;
                    foreach (var claim in anyClaims)
                    {
                        foreach (var claimSet in claimSets)
                        {
                            if (claimSet.ContainsClaim(claim))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            return false;
        }
    }
}
