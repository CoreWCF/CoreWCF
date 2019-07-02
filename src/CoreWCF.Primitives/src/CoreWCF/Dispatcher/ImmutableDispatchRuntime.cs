﻿using System;
using System.Threading.Tasks;
using System.Collections.Specialized;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using System.Collections.Generic;

namespace CoreWCF.Dispatcher
{
    class ImmutableDispatchRuntime
    {
        readonly AuthenticationBehavior _authenticationBehavior;
        readonly AuthorizationBehavior _authorizationBehavior;
        readonly int _correlationCount;
        readonly ConcurrencyBehavior _concurrency;
        readonly IDemuxer _demuxer;
        readonly ErrorBehavior _error;
        readonly bool _enableFaults;
        readonly bool impersonateOnSerializingReply;
        readonly IInputSessionShutdown[] _inputSessionShutdownHandlers;
        readonly bool _isOnServer;
        readonly bool _manualAddressing;
        readonly IDispatchMessageInspector[] _messageInspectors;
        readonly SecurityImpersonationBehavior securityImpersonation;
        readonly TerminatingOperationBehavior _terminate;
        readonly ThreadBehavior _thread;

        readonly MessageRpcErrorHandler _processMessageNonCleanupError;
        readonly MessageRpcErrorHandler _processMessageCleanupError;

        internal ImmutableDispatchRuntime(DispatchRuntime dispatch)
        {
            _authenticationBehavior = AuthenticationBehavior.TryCreate(dispatch);
            _authorizationBehavior = AuthorizationBehavior.TryCreate(dispatch);
            _concurrency = new ConcurrencyBehavior(dispatch);
            _error = new ErrorBehavior(dispatch.ChannelDispatcher);
            _enableFaults = dispatch.EnableFaults;
            _inputSessionShutdownHandlers = EmptyArray<IInputSessionShutdown>.ToArray(dispatch.InputSessionShutdownHandlers);
            InstanceBehavior = new InstanceBehavior(dispatch, this);
            _isOnServer = dispatch.IsOnServer;
            _manualAddressing = dispatch.ManualAddressing;
            _messageInspectors = EmptyArray<IDispatchMessageInspector>.ToArray(dispatch.MessageInspectors);
            // securityImpersonation = SecurityImpersonationBehavior.CreateIfNecessary(dispatch);
            impersonateOnSerializingReply = dispatch.ImpersonateOnSerializingReply;
            _terminate = TerminatingOperationBehavior.CreateIfNecessary(dispatch);
            _thread = new ThreadBehavior(dispatch);
            ValidateMustUnderstand = dispatch.ValidateMustUnderstand;
            ParameterInspectorCorrelationOffset = (dispatch.MessageInspectors.Count +
                dispatch.MaxCallContextInitializers);
            _correlationCount = ParameterInspectorCorrelationOffset + dispatch.MaxParameterInspectors;

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

        internal int CorrelationCount
        {
            get { return _correlationCount; }
        }

        internal bool EnableFaults
        {
            get { return _enableFaults; }
        }

        internal InstanceBehavior InstanceBehavior { get; }

        internal bool IsImpersonationEnabledOnSerializingReply
        {
            get { return impersonateOnSerializingReply; }
        }

        internal bool ManualAddressing
        {
            get { return _manualAddressing; }
        }

        internal int ParameterInspectorCorrelationOffset { get; }

        //        internal IRequestReplyCorrelator RequestReplyCorrelator
        //        {
        //            get { return this.requestReplyCorrelator; }
        //        }

        internal SecurityImpersonationBehavior SecurityImpersonation
        {
            get { return this.securityImpersonation; }
        }

        internal bool ValidateMustUnderstand { get; }

        internal ErrorBehavior ErrorBehavior
        {
            get { return _error; }
        }

        Task AcquireDynamicInstanceContextAsync(MessageRpc rpc)
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

        Task AcquireDynamicInstanceContextCoreAsync(MessageRpc rpc)
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

        void BeforeSendReply(MessageRpc rpc, ref Exception exception, ref bool thereIsAnUnhandledException)
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
                    thereIsAnUnhandledException = (!_error.HandleError(e)) || thereIsAnUnhandledException;
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
                _error.HandleError(e);
            }
            catch (TimeoutException e)
            {
                _error.HandleError(e);
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

                if (!_error.HandleError(e))
                {
                    rpc.RequestContextThrewOnReply = true;
                    rpc.CanSendReply = false;
                }
            }

            return rpc;
        }

        internal Task<MessageRpc> DispatchAsync(MessageRpc rpc, bool isOperationContextSet)
        {
            rpc.ErrorProcessor = _processMessageNonCleanupError;
            rpc.AsyncProcessor = ProcessMessageAsync;
            var task = rpc.ProcessAsync(isOperationContextSet);
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

        void InputSessionDoneReceivingCore(ServiceChannel channel)
        {
            IDuplexContextChannel proxy = channel.Proxy as IDuplexContextChannel;

            if (proxy != null)
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
                    if (!_error.HandleError(e))
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

        void InputSessionFaultedCore(ServiceChannel channel)
        {
            IDuplexContextChannel proxy = channel.Proxy as IDuplexContextChannel;

            if (proxy != null)
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
                    if (!_error.HandleError(e))
                    {
                        proxy.Abort();
                    }
                }
            }
        }

        void AddMessageProperties(Message message, OperationContext context, ServiceChannel replyChannel)
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

        void PrepareReply(MessageRpc rpc)
        {
            RequestContext context = rpc.OperationContext.RequestContext;
            Exception exception = null;
            bool thereIsAnUnhandledException = false;

            if (!rpc.Operation.IsOneWay)
            {
                //if (DiagnosticUtility.ShouldTraceWarning)
                //{
                //    // If a service both returns null and sets RequestContext null, that
                //    // means they handled it (either by calling Close or Reply manually).
                //    // These traces catch accidents, where you accidentally return null,
                //    // or you accidentally close the context so we can't return your message.
                //    if ((rpc.Reply == null) && (context != null))
                //    {
                //        TraceUtility.TraceEvent(System.Diagnostics.TraceEventType.Warning,
                //            TraceCode.ServiceOperationMissingReply,
                //            SR.Format(SR.TraceCodeServiceOperationMissingReply, rpc.Operation.Name ?? String.Empty),
                //            null, null);
                //    }
                //    else if ((context == null) && (rpc.Reply != null))
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
                        thereIsAnUnhandledException = (!_error.HandleError(e)) || thereIsAnUnhandledException;
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
                    _error.ProvideOnlyFaultOfLastResort(ref rpc);

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
                        _error.HandleError(e);
                    }
                }
            }
            else if ((exception != null) && thereIsAnUnhandledException)
            {
                rpc.Abort();
            }
        }

        bool PrepareAndAddressReply(ref MessageRpc rpc)
        {
            bool canSendReply = true;

            if (!_manualAddressing)
            {
                if (!object.ReferenceEquals(rpc.RequestID, null))
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
            //    rpc.Reply.Properties[EventTraceActivity.Name] = rpc.EventTraceActivity;
            //}

            return canSendReply;
        }

        internal DispatchOperationRuntime GetOperation(ref Message message)
        {
            return _demuxer.GetOperation(ref message);
        }

        internal async Task<MessageRpc> ProcessMessageAsync(MessageRpc rpc)
        {
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

                if (!_manualAddressing)
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

            // TODO: Make authenticationBehavior Async
            if (_authenticationBehavior != null)
            {
                _authenticationBehavior.Authenticate(ref rpc);
            }

            // TODO: Make authorizationBehavior Async
            if (_authorizationBehavior != null)
            {
                _authorizationBehavior.Authorize(ref rpc);
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
                if(!rpc._processCallReturned)
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

            try
            {
                _error.ProvideMessageFault(rpc);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                _error.HandleError(e);
            }

            PrepareReply(rpc);

            if (rpc.CanSendReply)
            {
                rpc.ReplyTimeoutHelper = new TimeoutHelper(rpc.Channel.OperationTimeout);
            }

            if (rpc.CanSendReply)
            {
                //if (rpc.Reply != null)
                //{
                //    TraceUtility.MessageFlowAtMessageSent(rpc.Reply, rpc.EventTraceActivity);
                //}

                rpc = await ReplyAsync(rpc);
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
        //                               Request           Reply
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
            Fx.Assert(
                !object.ReferenceEquals(rpc.ErrorProcessor, _processMessageCleanupError),
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
                    _error.HandleError(e);
                }

                rpc.DisposeParameters(false); //Dispose all input/output/return parameters

                if (rpc.FaultInfo.IsConsideredUnhandled)
                {
                    if (!replyWasSent)
                    {
                        rpc.AbortRequestContext();
                        rpc.AbortChannel();
                    }
                    else
                    {
                        rpc.CloseRequestContext();
                        rpc.CloseChannel();
                    }
                    rpc.AbortInstanceContext();
                }
                else
                {
                    if (rpc.RequestContextThrewOnReply)
                    {
                        rpc.AbortRequestContext();
                    }
                    else
                    {
                        rpc.CloseRequestContext();
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
                        _error.HandleError(e);
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
                        _error.HandleError(e);
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

                InstanceBehavior.AfterReply(ref rpc, _error);

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
                        _error.HandleError(e);
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
                        _error.HandleError(e);
                    }
                }

                if (rpc.SuccessfullyIncrementedActivity)
                {
                    try
                    {
                        rpc.Channel.DecrementActivity();
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        _error.HandleError(e);
                    }
                }
            }
            finally
            {
                // TODO: Add the code for the other half of InstanceContextServiceThrottle being acquired
                if (rpc.MessageRpcOwnsInstanceContextThrottle && rpc.channelHandler.InstanceContextServiceThrottle != null)
                {
                    rpc.channelHandler.InstanceContextServiceThrottle.DeactivateInstanceContext();
                }

                //if (rpc.Activity != null && DiagnosticUtility.ShouldUseActivity)
                //{
                //    rpc.Activity.Stop();
                //}
            }

            _error.HandleError(rpc);

            if (!_concurrency.IsConcurrent(rpc))
            {
                rpc.EnsureReceive();
            }

            return rpc;
        }

        void ProcessMessageNonCleanupError(MessageRpc rpc)
        {
            try
            {
                _error.ProvideMessageFault(rpc);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                _error.HandleError(e);
            }

            PrepareReply(rpc);
        }

        void ProcessMessageCleanupError(MessageRpc rpc)
        {
            _error.HandleError(rpc);
        }

        void SetActivityIdOnThread(MessageRpc rpc)
        {
            //if (FxTrace.Trace.IsEnd2EndActivityTracingEnabled && rpc.EventTraceActivity != null)
            //{
            //    // Propogate the ActivityId to the service operation
            //    EventTraceActivityHelper.SetOnThread(rpc.EventTraceActivity);
            //}
        }

        void TransferChannelFromPendingList(MessageRpc rpc)
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

        interface IDemuxer
        {
            DispatchOperationRuntime GetOperation(ref Message request);
        }

        class ActionDemuxer : IDemuxer
        {
            HybridDictionary map;
            DispatchOperationRuntime unhandled;

            internal ActionDemuxer()
            {
                map = new HybridDictionary();
            }

            internal void Add(string action, DispatchOperationRuntime operation)
            {
                if (map.Contains(action))
                {
                    DispatchOperationRuntime existingOperation = (DispatchOperationRuntime)map[action];
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxActionDemuxerDuplicate, existingOperation.Name, operation.Name, action)));
                }
                map.Add(action, operation);
            }

            internal void SetUnhandled(DispatchOperationRuntime operation)
            {
                unhandled = operation;
            }

            public DispatchOperationRuntime GetOperation(ref Message request)
            {
                string action = request.Headers.Action;
                if (action == null)
                {
                    action = MessageHeaders.WildcardAction;
                }
                DispatchOperationRuntime operation = (DispatchOperationRuntime)map[action];
                if (operation != null)
                {
                    return operation;
                }

                return unhandled;
            }
        }

        class CustomDemuxer : IDemuxer
        {
            Dictionary<string, DispatchOperationRuntime> _map;
            IDispatchOperationSelector _selector;
            DispatchOperationRuntime _unhandled;

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
                DispatchOperationRuntime operation = null;
                if (_map.TryGetValue(operationName, out operation))
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