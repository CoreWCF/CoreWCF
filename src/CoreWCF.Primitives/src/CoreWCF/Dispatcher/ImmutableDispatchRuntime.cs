// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class ImmutableDispatchRuntime
    {
        private readonly AuthenticationBehavior _authenticationBehavior;
        private readonly AuthorizationBehavior _authorizationBehavior;
        private readonly ConcurrencyBehavior _concurrency;
        private readonly IDemuxer _demuxer;
        private readonly IInputSessionShutdown[] _inputSessionShutdownHandlers;
        private readonly bool _isOnServer;
        private readonly IDispatchMessageInspector[] _messageInspectors;
        private readonly TerminatingOperationBehavior _terminate;
        private readonly ThreadBehavior _thread;
        private readonly MessageRpcErrorHandler _processMessageNonCleanupError;
        private readonly MessageRpcErrorHandler _processMessageCleanupError;

        internal ImmutableDispatchRuntime(DispatchRuntime dispatch)
        {
            _authenticationBehavior = AuthenticationBehavior.TryCreate(dispatch);
            _authorizationBehavior = AuthorizationBehavior.TryCreate(dispatch);
            _concurrency = new ConcurrencyBehavior(dispatch);
            ErrorBehavior = new ErrorBehavior(dispatch.ChannelDispatcher);
            EnableFaults = dispatch.EnableFaults;
            _inputSessionShutdownHandlers = EmptyArray<IInputSessionShutdown>.ToArray(dispatch.InputSessionShutdownHandlers);
            InstanceBehavior = new InstanceBehavior(dispatch, this);
            _isOnServer = dispatch.IsOnServer;
            ManualAddressing = dispatch.ManualAddressing;
            _messageInspectors = EmptyArray<IDispatchMessageInspector>.ToArray(dispatch.MessageInspectors);
            SecurityImpersonation = SecurityImpersonationBehavior.CreateIfNecessary(dispatch);
            RequireClaimsPrincipalOnOperationContext = dispatch.RequireClaimsPrincipalOnOperationContext;
            SupportsAuthorizationData = dispatch.SupportsAuthorizationData;
            IsImpersonationEnabledOnSerializingReply = dispatch.ImpersonateOnSerializingReply;
            _terminate = TerminatingOperationBehavior.CreateIfNecessary(dispatch);
            _thread = new ThreadBehavior(dispatch);
            ValidateMustUnderstand = dispatch.ValidateMustUnderstand;
            ParameterInspectorCorrelationOffset = (dispatch.MessageInspectors.Count +
                dispatch.MaxCallContextInitializers);
            CorrelationCount = ParameterInspectorCorrelationOffset + dispatch.MaxParameterInspectors;

            DispatchOperationRuntime unhandled = new DispatchOperationRuntime(dispatch.UnhandledDispatchOperation, this);

            if (dispatch.OperationSelector == null)
            {
                ActionDemuxer demuxer = new ActionDemuxer();
                for (int i = 0; i < dispatch.Operations.Count; i++)
                {
                    DispatchOperation operation = dispatch.Operations[i];
                    DispatchOperationRuntime operationRuntime = new DispatchOperationRuntime(operation, this);
                    demuxer.Add(operation.Action, operationRuntime);
                }

                demuxer.SetUnhandled(unhandled);
                _demuxer = demuxer;
            }
            else
            {
                CustomDemuxer demuxer = new CustomDemuxer(dispatch.OperationSelector);
                for (int i = 0; i < dispatch.Operations.Count; i++)
                {
                    DispatchOperation operation = dispatch.Operations[i];
                    DispatchOperationRuntime operationRuntime = new DispatchOperationRuntime(operation, this);
                    demuxer.Add(operation.Name, operationRuntime);
                }

                demuxer.SetUnhandled(unhandled);
                _demuxer = demuxer;
            }

            _processMessageNonCleanupError = new MessageRpcErrorHandler(ProcessMessageNonCleanupError);
            _processMessageCleanupError = new MessageRpcErrorHandler(ProcessMessageCleanupError);
        }

        internal int CallContextCorrelationOffset
        {
            get { return _messageInspectors.Length; }
        }

        internal int CorrelationCount { get; }

        internal bool EnableFaults { get; }

        internal InstanceBehavior InstanceBehavior { get; }

        internal bool IsImpersonationEnabledOnSerializingReply { get; }

        internal bool RequireClaimsPrincipalOnOperationContext { get; }

        internal bool SupportsAuthorizationData { get; }

        internal bool ManualAddressing { get; }

        internal int ParameterInspectorCorrelationOffset { get; }

        //        internal IRequestReplyCorrelator RequestReplyCorrelator
        //        {
        //            get { return this.requestReplyCorrelator; }
        //        }

        internal SecurityImpersonationBehavior SecurityImpersonation { get; }

        internal bool ValidateMustUnderstand { get; }

        internal ErrorBehavior ErrorBehavior { get; }

        private Task AcquireDynamicInstanceContextAsync(MessageRpc rpc)
        {
            if (rpc.InstanceContext.QuotaThrottle != null)
            {
                return AcquireDynamicInstanceContextCoreAsync(rpc);
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        private Task AcquireDynamicInstanceContextCoreAsync(MessageRpc rpc)
        {
            return rpc.InstanceContext.QuotaThrottle.AcquireAsync();
        }

        internal void AfterReceiveRequest(ref MessageRpc rpc)
        {
            if (_messageInspectors.Length > 0)
            {
                AfterReceiveRequestCore(ref rpc);
            }
        }

        internal void AfterReceiveRequestCore(ref MessageRpc rpc)
        {
            try
            {
                for (int i = 0; i < _messageInspectors.Length; i++)
                {
                    rpc.Correlation[i] = _messageInspectors[i].AfterReceiveRequest(ref rpc.Request, (IClientChannel)rpc.Channel.Proxy, rpc.InstanceContext);
                    //if (TD.MessageInspectorAfterReceiveInvokedIsEnabled())
                    //{
                    //    TD.MessageInspectorAfterReceiveInvoked(rpc.EventTraceActivity, this.messageInspectors[i].GetType().FullName);
                    //}
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

        private void BeforeSendReply(MessageRpc rpc, ref Exception exception, ref bool thereIsAnUnhandledException)
        {
            if (_messageInspectors.Length > 0)
            {
                BeforeSendReplyCore(rpc, ref exception, ref thereIsAnUnhandledException);
            }
        }

        internal void BeforeSendReplyCore(MessageRpc rpc, ref Exception exception, ref bool thereIsAnUnhandledException)
        {
            for (int i = 0; i < _messageInspectors.Length; i++)
            {
                try
                {
                    Message originalReply = rpc.Reply;
                    Message reply = originalReply;

                    _messageInspectors[i].BeforeSendReply(ref reply, rpc.Correlation[i]);
                    //if (TD.MessageInspectorBeforeSendInvokedIsEnabled())
                    //{
                    //    TD.MessageInspectorBeforeSendInvoked(rpc.EventTraceActivity, this.messageInspectors[i].GetType().FullName);
                    //}

                    if ((reply == null) && (originalReply != null))
                    {
                        string message = SR.Format(SR.SFxNullReplyFromExtension2, _messageInspectors[i].GetType().ToString(), (rpc.Operation.Name ?? ""));
                        ErrorBehavior.ThrowAndCatch(new InvalidOperationException(message));
                    }
                    rpc.Reply = reply;
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    if (!ErrorBehavior.ShouldRethrowExceptionAsIs(e))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
                    }

                    if (exception == null)
                    {
                        exception = e;
                    }
                    thereIsAnUnhandledException = (!ErrorBehavior.HandleError(e)) || thereIsAnUnhandledException;
                }
            }
        }

        private async Task<MessageRpc> ReplyAsync(MessageRpc rpc)
        {
            rpc.RequestContextThrewOnReply = true;
            rpc.SuccessfullySendReply = false;

            try
            {
                await rpc.RequestContext.ReplyAsync(rpc.Reply, rpc.ReplyTimeoutHelper.GetCancellationToken());
                rpc.RequestContextThrewOnReply = false;
                rpc.SuccessfullySendReply = true;

                //if (TD.DispatchMessageStopIsEnabled())
                //{
                //    TD.DispatchMessageStop(rpc.EventTraceActivity);
                //}
            }
            catch (CommunicationException e)
            {
                ErrorBehavior.HandleError(e);
            }
            catch (TimeoutException e)
            {
                ErrorBehavior.HandleError(e);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                //if (DiagnosticUtility.ShouldTraceError)
                //{
                //    TraceUtility.TraceEvent(TraceEventType.Error, TraceCode.ServiceOperationExceptionOnReply,
                //        SR.Format(SR.TraceCodeServiceOperationExceptionOnReply),
                //        this, e);
                //}

                if (!ErrorBehavior.HandleError(e))
                {
                    rpc.RequestContextThrewOnReply = true;
                    rpc.CanSendReply = false;
                }
            }

            return rpc;
        }

        internal Task<MessageRpc> DispatchAsync(MessageRpc rpc, bool isOperationContextSet)
        {
            rpc.ErrorProcessor = ProcessError;
            rpc.AsyncProcessor = ProcessMessageAsync;
            Task<MessageRpc> task = rpc.ProcessAsync(isOperationContextSet);
            rpc._processCallReturned = true;
            return task;
        }

        internal void InputSessionDoneReceiving(ServiceChannel channel)
        {
            if (_inputSessionShutdownHandlers.Length > 0)
            {
                InputSessionDoneReceivingCore(channel);
            }
        }

        private void InputSessionDoneReceivingCore(ServiceChannel channel)
        {
            if (channel.Proxy is IDuplexContextChannel proxy)
            {
                IInputSessionShutdown[] handlers = _inputSessionShutdownHandlers;
                try
                {
                    for (int i = 0; i < handlers.Length; i++)
                    {
                        handlers[i].DoneReceiving(proxy);
                    }
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    if (!ErrorBehavior.HandleError(e))
                    {
                        proxy.Abort();
                    }
                }
            }
        }

        internal bool IsConcurrent(MessageRpc rpc)
        {
            return _concurrency.IsConcurrent(rpc);
        }

        internal void InputSessionFaulted(ServiceChannel channel)
        {
            if (_inputSessionShutdownHandlers.Length > 0)
            {
                InputSessionFaultedCore(channel);
            }
        }

        private void InputSessionFaultedCore(ServiceChannel channel)
        {
            if (channel.Proxy is IDuplexContextChannel proxy)
            {
                IInputSessionShutdown[] handlers = _inputSessionShutdownHandlers;
                try
                {
                    for (int i = 0; i < handlers.Length; i++)
                    {
                        handlers[i].ChannelFaulted(proxy);
                    }
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    if (!ErrorBehavior.HandleError(e))
                    {
                        proxy.Abort();
                    }
                }
            }
        }

        private void AddMessageProperties(Message message, OperationContext context, ServiceChannel replyChannel)
        {
            if (context.InternalServiceChannel == replyChannel)
            {
                if (context.HasOutgoingMessageHeaders)
                {
                    message.Headers.CopyHeadersFrom(context.OutgoingMessageHeaders);
                }

                if (context.HasOutgoingMessageProperties)
                {
                    message.Properties.MergeProperties(context.OutgoingMessageProperties);
                }
            }
        }

        private async ValueTask PrepareReplyAsync(MessageRpc rpc)
        {
            RequestContext context = rpc.OperationContext.RequestContext;
            Exception exception = null;
            bool thereIsAnUnhandledException = false;

            if (!rpc.Operation.IsOneWay)
            {
                //if (DiagnosticUtility.ShouldTraceWarning)
                //{
                //    // If a service both returns null and sets RequestContext null, that
                //    // means they handled it (either by calling Close or ReplyAsync manually).
                //    // These traces catch accidents, where you accidentally return null,
                //    // or you accidentally close the context so we can't return your message.
                //    if ((rpc.ReplyAsync == null) && (context != null))
                //    {
                //        TraceUtility.TraceEvent(System.Diagnostics.TraceEventType.Warning,
                //            TraceCode.ServiceOperationMissingReply,
                //            SR.Format(SR.TraceCodeServiceOperationMissingReply, rpc.Operation.Name ?? String.Empty),
                //            null, null);
                //    }
                //    else if ((context == null) && (rpc.ReplyAsync != null))
                //    {
                //        TraceUtility.TraceEvent(System.Diagnostics.TraceEventType.Warning,
                //            TraceCode.ServiceOperationMissingReplyContext,
                //            SR.Format(SR.TraceCodeServiceOperationMissingReplyContext, rpc.Operation.Name ?? String.Empty),
                //            null, null);
                //    }
                //}

                if ((context != null) && (rpc.Reply != null))
                {
                    try
                    {
                        rpc.CanSendReply = PrepareAndAddressReply(ref rpc);
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        thereIsAnUnhandledException = (!ErrorBehavior.HandleError(e)) || thereIsAnUnhandledException;
                        exception = e;
                    }
                }
            }

            BeforeSendReply(rpc, ref exception, ref thereIsAnUnhandledException);

            if (rpc.Operation.IsOneWay)
            {
                rpc.CanSendReply = false;
            }

            if (!rpc.Operation.IsOneWay && (context != null) && (rpc.Reply != null))
            {
                if (exception != null)
                {
                    // We don't call ProvideFault again, since we have already passed the
                    // point where SFx addresses the reply, and it is reasonable for
                    // ProvideFault to expect that SFx will address the reply.  Instead
                    // we always just do 'internal server error' processing.
                    rpc.Error = exception;
                    ErrorBehavior.ProvideOnlyFaultOfLastResort(ref rpc);

                    try
                    {
                        rpc.CanSendReply = PrepareAndAddressReply(ref rpc);
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        ErrorBehavior.HandleError(e);
                    }
                }
            }
            else if ((exception != null) && thereIsAnUnhandledException)
            {
                await rpc.AbortAsync();
            }
        }

        private bool PrepareAndAddressReply(ref MessageRpc rpc)
        {
            bool canSendReply = true;

            if (!ManualAddressing)
            {
                if (!ReferenceEquals(rpc.RequestID, null))
                {
                    RequestReplyCorrelator.PrepareReply(rpc.Reply, rpc.RequestID);
                }

                if (!rpc.Channel.HasSession)
                {
                    canSendReply = RequestReplyCorrelator.AddressReply(rpc.Reply, rpc.ReplyToInfo);
                }
            }

            AddMessageProperties(rpc.Reply, rpc.OperationContext, rpc.Channel);
            //if (FxTrace.Trace.IsEnd2EndActivityTracingEnabled && rpc.EventTraceActivity != null)
            //{
            //    rpc.ReplyAsync.Properties[EventTraceActivity.Name] = rpc.EventTraceActivity;
            //}

            return canSendReply;
        }

        internal DispatchOperationRuntime GetOperation(ref Message message)
        {
            return _demuxer.GetOperation(ref message);
        }

        private void ReceiveContextRPCFacet_CreatIfRequired_Shim(MessageRpc rpc)
        {
            rpc.ReceiveContext = ReceiveContext.TryGet(rpc.Request, out ReceiveContext receiveContext)
                ? receiveContext
                : null;
        }

        internal async Task<MessageRpc> ProcessMessageAsync(MessageRpc rpc)
        {
            ReceiveContextRPCFacet_CreatIfRequired_Shim(rpc);

            if (rpc.Operation.IsOneWay)
            {
                await rpc.RequestContext.ReplyAsync(null);
                rpc.OperationContext.RequestContext = null;
            }
            else
            {
                if (!rpc.Channel.IsReplyChannel &&
                    ((object)rpc.RequestID == null) &&
                    (rpc.Operation.Action != MessageHeaders.WildcardAction))
                {
                    CommunicationException error = new CommunicationException(SR.SFxOneWayMessageToTwoWayMethod0);
                    throw TraceUtility.ThrowHelperError(error, rpc.Request);
                }

                if (!ManualAddressing)
                {
                    EndpointAddress replyTo = rpc.ReplyToInfo.ReplyTo;
                    if (replyTo != null && replyTo.IsNone && rpc.Channel.IsReplyChannel)
                    {
                        CommunicationException error = new CommunicationException(SR.SFxRequestReplyNone);
                        throw TraceUtility.ThrowHelperError(error, rpc.Request);
                    }

                    if (_isOnServer)
                    {
                        EndpointAddress remoteAddress = rpc.Channel.RemoteAddress;
                        if ((remoteAddress != null) && !remoteAddress.IsAnonymous)
                        {
                            MessageHeaders headers = rpc.Request.Headers;
                            Uri remoteUri = remoteAddress.Uri;

                            if ((replyTo != null) && !replyTo.IsAnonymous && (remoteUri != replyTo.Uri))
                            {
                                string text = SR.Format(SR.SFxRequestHasInvalidReplyToOnServer, replyTo.Uri, remoteUri);
                                Exception error = new InvalidOperationException(text);
                                throw TraceUtility.ThrowHelperError(error, rpc.Request);
                            }

                            EndpointAddress faultTo = headers.FaultTo;
                            if ((faultTo != null) && !faultTo.IsAnonymous && (remoteUri != faultTo.Uri))
                            {
                                string text = SR.Format(SR.SFxRequestHasInvalidFaultToOnServer, faultTo.Uri, remoteUri);
                                Exception error = new InvalidOperationException(text);
                                throw TraceUtility.ThrowHelperError(error, rpc.Request);
                            }

                            if (rpc.RequestVersion.Addressing == AddressingVersion.WSAddressingAugust2004)
                            {
                                EndpointAddress from = headers.From;
                                if ((from != null) && !from.IsAnonymous && (remoteUri != from.Uri))
                                {
                                    string text = SR.Format(SR.SFxRequestHasInvalidFromOnServer, from.Uri, remoteUri);
                                    Exception error = new InvalidOperationException(text);
                                    throw TraceUtility.ThrowHelperError(error, rpc.Request);
                                }
                            }
                        }
                    }
                }
            }

            if (_concurrency.IsConcurrent(rpc))
            {
                rpc.Channel.IncrementActivity();
                rpc.SuccessfullyIncrementedActivity = true;
            }

            if (_authenticationBehavior != null)
            {
                rpc = await _authenticationBehavior.AuthenticateAsync(rpc);
            }

            if (_authorizationBehavior != null && !SupportsAuthorizationData)
            {
                rpc = await _authorizationBehavior.AuthorizeAsync(rpc);
            }

            await InstanceBehavior.EnsureInstanceContextAsync(rpc);
            TransferChannelFromPendingList(rpc);
            await AcquireDynamicInstanceContextAsync(rpc);

            AfterReceiveRequest(ref rpc);

            await _concurrency.LockInstanceAsync(rpc);
            rpc.SuccessfullyLockedInstance = true;

            try
            {
                // TaskHelpers has an extension method which enables awaitting a sync context to run continuation on it.
                await _thread.GetSyncContext(rpc);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperFatal(e.Message, e);
            }

            // This needs to happen after LockInstance--LockInstance guarantees
            // in-order delivery, so we can't receive the next message until we
            // have acquired the lock.
            //
            // This also needs to happen after BindThread based on the assumption
            // that running on UI thread should guarantee in-order delivery if
            // the SynchronizationContext is single threaded.
            if (_concurrency.IsConcurrent(rpc))
            {
                rpc.EnsureReceive();
                if (!rpc._processCallReturned)
                {
                    // To allow transport receive loop to get next request, the call to dispatch the current message needs to return.
                    // If all previous await's have completed synchronously, execution needs to be forced to continue on another thread.
                    // This code causes this method to continue on another thread and any calling receive pump (such as NetTcp) will
                    // use this thread to request the next message. It might be better to switch that so this thread continues on this
                    // thread and the caller has to run on a new thread.
                    await Task.Yield();
                }
            }

            InstanceBehavior.EnsureServiceInstance(rpc);

            if (RequireClaimsPrincipalOnOperationContext)
            {
                rpc.Operation.SetClaimsPrincipalToOperationContext(rpc);
            }

            if (_authorizationBehavior != null && SupportsAuthorizationData)
            {
                rpc = await _authorizationBehavior.AuthorizePolicyAsync(rpc);
            }

            try
            {
                SetActivityIdOnThread(rpc);
                rpc = await rpc.Operation.InvokeAsync(rpc);
            }
            catch
            {
                // This catch clause forces ClearCallContext to run prior to stackwalks exiting this frame.
                throw;
            }

            try
            {
                // Switch back to thread pool if we're using a non-default Sync Context. This only switches threads if needed.
                await TaskHelpers.EnsureDefaultTaskScheduler();
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperFatal(e.Message, e);
            }

            await ProcessError(rpc);

            if (!_concurrency.IsConcurrent(rpc))
            {
                rpc.EnsureReceive();
            }

            return rpc;
        }

        private async Task ProcessError(MessageRpc rpc)
        {
            try
            {
                ErrorBehavior.ProvideMessageFault(rpc);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                ErrorBehavior.HandleError(e);
            }

            await PrepareReplyAsync(rpc);

            if (rpc.CanSendReply)
            {
                rpc.ReplyTimeoutHelper = new TimeoutHelper(rpc.Channel.OperationTimeout);
                //if (rpc.ReplyAsync != null)
                //{
                //    TraceUtility.MessageFlowAtMessageSent(rpc.ReplyAsync, rpc.EventTraceActivity);
                //}

                await ReplyAsync(rpc);
            }

            await ProcessMessageCleanupAsync(rpc);
        }

        // Logic for knowing when to close stuff:
        //
        // ASSUMPTIONS:
        //   Closing a stream over a message also closes the message.
        //   Closing a message over a stream does not close the stream.
        //     (OperationStreamProvider.ReleaseStream is no-op)
        //
        // This is a table of what should be disposed in what cases.
        // The rows represent the type of parameter to the method and
        // whether we are disposing parameters or not.  The columns
        // are for the inputs vs. the outputs.  The cells contain the
        // values that need to be Disposed.  M^P means that exactly
        // one of the message and parameter needs to be disposed,
        // since they refer to the same object.
        //
        //                               Request           ReplyAsync
        //               Message   |     M or P      |     M or P
        //     Dispose   Stream    |     P           |     M and P
        //               Params    |     M and P     |     M and P
        //                         |                 |
        //               Message   |     none        |     none
        //   NoDispose   Stream    |     none        |     M
        //               Params    |     M           |     M
        //
        // By choosing to dispose the parameter in both of the "M or P"
        // cases, the logic needed to generate this table is:
        //
        // CloseRequestMessage = IsParams
        // CloseRequestParams  = rpc.Operation.DisposeParameters
        // CloseReplyMessage   = rpc.Operation.SerializeReply
        // CloseReplyParams    = rpc.Operation.DisposeParameters
        //
        // IsParams can be calculated based on whether the request
        // message was consumed after deserializing but before calling
        // the user.  This is stored as rpc.DidDeserializeRequestBody.
        //
        private async Task ProcessMessageCleanupAsync(MessageRpc rpc)
        {
            Fx.Assert(
                !ReferenceEquals(rpc.ErrorProcessor, _processMessageCleanupError),
                "ProcessMessageCleanup run twice on the same MessageRpc!");
            rpc.ErrorProcessor = _processMessageCleanupError;

            bool replyWasSent = false;

            if (rpc.CanSendReply)
            {
                replyWasSent = rpc.SuccessfullySendReply;
            }

            try
            {
                try
                {
                    if (rpc.DidDeserializeRequestBody)
                    {
                        rpc.Request.Close();
                    }
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    ErrorBehavior.HandleError(e);
                }

                rpc.DisposeParameters(false); //Dispose all input/output/return parameters

                if (rpc.FaultInfo.IsConsideredUnhandled)
                {
                    if (!replyWasSent)
                    {
                        await rpc.AbortRequestContextAsync();
                        rpc.AbortChannel();
                    }
                    else
                    {
                        await rpc.CloseRequestContextAsync();
                        await rpc.CloseChannelAsync();
                    }
                    rpc.AbortInstanceContext();
                }
                else
                {
                    if (rpc.RequestContextThrewOnReply)
                    {
                        await rpc.AbortRequestContextAsync();
                    }
                    else
                    {
                        await rpc.CloseRequestContextAsync();
                    }
                }

                if ((rpc.Reply != null) && (rpc.Reply != rpc.ReturnParameter))
                {
                    try
                    {
                        rpc.Reply.Close();
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        ErrorBehavior.HandleError(e);
                    }
                }

                if ((rpc.FaultInfo.Fault != null) && (rpc.FaultInfo.Fault.State != MessageState.Closed))
                {
                    // maybe ProvideFault gave a Message, but then BeforeSendReply replaced it
                    // in that case, we need to close the one from ProvideFault
                    try
                    {
                        rpc.FaultInfo.Fault.Close();
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        ErrorBehavior.HandleError(e);
                    }
                }

                try
                {
                    rpc.OperationContext.FireOperationCompleted();
                }

                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
                }

                InstanceBehavior.AfterReply(ref rpc, ErrorBehavior);

                if (rpc.SuccessfullyLockedInstance)
                {
                    try
                    {
                        _concurrency.UnlockInstance(ref rpc);
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }

                        Fx.Assert("Exceptions should be caught by callee");
                        rpc.InstanceContext.FaultInternal();
                        ErrorBehavior.HandleError(e);
                    }
                }

                if (_terminate != null)
                {
                    try
                    {
                        _terminate.AfterReply(ref rpc);
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        ErrorBehavior.HandleError(e);
                    }
                }

                if (rpc.SuccessfullyIncrementedActivity)
                {
                    try
                    {
                        await rpc.Channel.DecrementActivityAsync();
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        ErrorBehavior.HandleError(e);
                    }
                }
            }
            finally
            {
                // TODO: Add the code for the other half of InstanceContextServiceThrottle being acquired
                if (rpc.MessageRpcOwnsInstanceContextThrottle && rpc.ChannelHandler.InstanceContextServiceThrottle != null)
                {
                    rpc.ChannelHandler.InstanceContextServiceThrottle.DeactivateInstanceContext();
                }

                //if (rpc.Activity != null && DiagnosticUtility.ShouldUseActivity)
                //{
                //    rpc.Activity.Stop();
                //}
            }

            ErrorBehavior.HandleError(rpc);
        }

        private async Task ProcessMessageNonCleanupError(MessageRpc rpc)
        {
            try
            {
                ErrorBehavior.ProvideMessageFault(rpc);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                ErrorBehavior.HandleError(e);
            }

            await PrepareReplyAsync(rpc);
        }

        private Task ProcessMessageCleanupError(MessageRpc rpc)
        {
            ErrorBehavior.HandleError(rpc);
            return Task.CompletedTask;
        }

        private void SetActivityIdOnThread(MessageRpc rpc)
        {
            //if (FxTrace.Trace.IsEnd2EndActivityTracingEnabled && rpc.EventTraceActivity != null)
            //{
            //    // Propogate the ActivityId to the service operation
            //    EventTraceActivityHelper.SetOnThread(rpc.EventTraceActivity);
            //}
        }

        private void TransferChannelFromPendingList(MessageRpc rpc)
        {
            if (rpc.Channel.IsPending)
            {
                rpc.Channel.IsPending = false;

                ChannelDispatcher channelDispatcher = rpc.Channel.ChannelDispatcher;
                IInstanceContextProvider provider = InstanceBehavior.InstanceContextProvider;

                if (!InstanceContextProviderBase.IsProviderSessionful(provider) &&
                    !InstanceContextProviderBase.IsProviderSingleton(provider))
                {
                    IChannel proxy = rpc.Channel.Proxy as IChannel;
                    if (!rpc.InstanceContext.IncomingChannels.Contains(proxy))
                    {
                        channelDispatcher.Channels.Add(proxy);
                    }
                }

                // TODO: Do we need to keep track of pending channels with the new hosting model?
                //channelDispatcher.PendingChannels.Remove(rpc.Channel.Binder.Channel);
            }
        }

        private interface IDemuxer
        {
            DispatchOperationRuntime GetOperation(ref Message request);
        }

        private class ActionDemuxer : IDemuxer
        {
            private readonly HybridDictionary _map;
            private DispatchOperationRuntime _unhandled;

            internal ActionDemuxer()
            {
                _map = new HybridDictionary();
            }

            internal void Add(string action, DispatchOperationRuntime operation)
            {
                if (_map.Contains(action))
                {
                    DispatchOperationRuntime existingOperation = (DispatchOperationRuntime)_map[action];
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxActionDemuxerDuplicate, existingOperation.Name, operation.Name, action)));
                }
                _map.Add(action, operation);
            }

            internal void SetUnhandled(DispatchOperationRuntime operation)
            {
                _unhandled = operation;
            }

            public DispatchOperationRuntime GetOperation(ref Message request)
            {
                string action = request.Headers.Action;
                if (action == null)
                {
                    action = MessageHeaders.WildcardAction;
                }
                DispatchOperationRuntime operation = (DispatchOperationRuntime)_map[action];
                if (operation != null)
                {
                    return operation;
                }

                return _unhandled;
            }
        }

        private class CustomDemuxer : IDemuxer
        {
            private readonly Dictionary<string, DispatchOperationRuntime> _map;
            private readonly IDispatchOperationSelector _selector;
            private DispatchOperationRuntime _unhandled;

            internal CustomDemuxer(IDispatchOperationSelector selector)
            {
                _selector = selector;
                _map = new Dictionary<string, DispatchOperationRuntime>();
            }

            internal void Add(string name, DispatchOperationRuntime operation)
            {
                _map.Add(name, operation);
            }

            internal void SetUnhandled(DispatchOperationRuntime operation)
            {
                _unhandled = operation;
            }

            public DispatchOperationRuntime GetOperation(ref Message request)
            {
                string operationName = _selector.SelectOperation(ref request);
                if (_map.TryGetValue(operationName, out DispatchOperationRuntime operation))
                {
                    return operation;
                }
                else
                {
                    return _unhandled;
                }
            }
        }
    }
}
