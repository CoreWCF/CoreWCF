using System;
using System.Threading.Tasks;
using System.Collections.Specialized;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;

namespace CoreWCF.Dispatcher
{
    class ImmutableDispatchRuntime
    {
        readonly AuthenticationBehavior authenticationBehavior;
        readonly AuthorizationBehavior authorizationBehavior;
        readonly int correlationCount;
        readonly ConcurrencyBehavior concurrency;
        readonly IDemuxer demuxer;
        readonly ErrorBehavior error;
        readonly bool enableFaults;
        //readonly bool impersonateOnSerializingReply;
        readonly IInputSessionShutdown[] inputSessionShutdownHandlers;
        readonly InstanceBehavior instance;
        readonly bool isOnServer;
        readonly bool manualAddressing;
        readonly IDispatchMessageInspector[] messageInspectors;
        //readonly SecurityImpersonationBehavior securityImpersonation;
        readonly TerminatingOperationBehavior terminate;
        readonly ThreadBehavior thread;
        //readonly bool sendAsynchronously;

        readonly MessageRpcErrorHandler processMessageNonCleanupError;
        readonly MessageRpcErrorHandler processMessageCleanupError;

        //static AsyncCallback onFinalizeCorrelationCompleted =
        //    Fx.ThunkCallback(new AsyncCallback(OnFinalizeCorrelationCompletedCallback));
        // TODO: Put the ThunkCallback back in
        static Action<Task,object> onReplyCompleted = OnReplyCompletedCallback;

        internal ImmutableDispatchRuntime(DispatchRuntime dispatch)
        {
            authenticationBehavior = AuthenticationBehavior.TryCreate(dispatch);
            authorizationBehavior = AuthorizationBehavior.TryCreate(dispatch);
            concurrency = new ConcurrencyBehavior(dispatch);
            error = new ErrorBehavior(dispatch.ChannelDispatcher);
            enableFaults = dispatch.EnableFaults;
            inputSessionShutdownHandlers = EmptyArray<IInputSessionShutdown>.ToArray(dispatch.InputSessionShutdownHandlers);
            instance = new InstanceBehavior(dispatch, this);
            isOnServer = dispatch.IsOnServer;
            manualAddressing = dispatch.ManualAddressing;
            messageInspectors = EmptyArray<IDispatchMessageInspector>.ToArray(dispatch.MessageInspectors);
            //this.requestReplyCorrelator = new RequestReplyCorrelator();
            //this.securityImpersonation = SecurityImpersonationBehavior.CreateIfNecessary(dispatch);
            //this.RequireClaimsPrincipalOnOperationContext = dispatch.RequireClaimsPrincipalOnOperationContext;
            //this.impersonateOnSerializingReply = dispatch.ImpersonateOnSerializingReply;
            terminate = TerminatingOperationBehavior.CreateIfNecessary(dispatch);
            thread = new ThreadBehavior(dispatch);
            ValidateMustUnderstand = dispatch.ValidateMustUnderstand;
            //sendAsynchronously = dispatch.ChannelDispatcher.SendAsynchronously;
            ParameterInspectorCorrelationOffset = (dispatch.MessageInspectors.Count +
                dispatch.MaxCallContextInitializers);
            correlationCount = ParameterInspectorCorrelationOffset + dispatch.MaxParameterInspectors;

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
                this.demuxer = demuxer;
            }
            else
            {
                throw new PlatformNotSupportedException();
                //    CustomDemuxer demuxer = new CustomDemuxer(dispatch.OperationSelector);
                //    for (int i = 0; i < dispatch.Operations.Count; i++)
                //    {
                //        DispatchOperation operation = dispatch.Operations[i];
                //        DispatchOperationRuntime operationRuntime = new DispatchOperationRuntime(operation, this);
                //        demuxer.Add(operation.Name, operationRuntime);
                //    }

                //    demuxer.SetUnhandled(unhandled);
                //    this.demuxer = demuxer;
            }

            //processMessage1 = new MessageRpcProcessor(ProcessMessage1);
            //processMessage11 = new MessageRpcProcessor(ProcessMessage11);
            //processMessage2 = new MessageRpcProcessor(ProcessMessage2);
            //processMessage3 = new MessageRpcProcessor(ProcessMessage3);
            //processMessage31 = new MessageRpcProcessor(ProcessMessage31);
            //processMessage4 = new MessageRpcProcessor(ProcessMessage4);
            //processMessage41 = new MessageRpcProcessor(ProcessMessage41);
            //processMessage5 = new MessageRpcProcessor(ProcessMessage5);
            //processMessage6 = new MessageRpcProcessor(ProcessMessage6);
            //processMessage7 = new MessageRpcProcessor(ProcessMessage7);
            //processMessage8 = new MessageRpcProcessor(ProcessMessage8);
            //processMessage9 = new MessageRpcProcessor(ProcessMessage9);
            //processMessageCleanup = new MessageRpcProcessor(ProcessMessageCleanup);
            processMessageNonCleanupError = new MessageRpcErrorHandler(ProcessMessageNonCleanupError);
            processMessageCleanupError = new MessageRpcErrorHandler(ProcessMessageCleanupError);
        }

        internal int CallContextCorrelationOffset
        {
            get { return messageInspectors.Length; }
        }

        internal int CorrelationCount
        {
            get { return correlationCount; }
        }

        internal bool EnableFaults
        {
            get { return enableFaults; }
        }

        //        internal InstanceBehavior InstanceBehavior
        //        {
        //            get { return this.instance; }
        //        }

        //        internal bool IsImpersonationEnabledOnSerializingReply
        //        {
        //            get { return this.impersonateOnSerializingReply; }
        //        }

        internal bool RequireClaimsPrincipalOnOperationContext { get; }

        internal bool ManualAddressing
        {
            get { return manualAddressing; }
        }

        // TODO: Do we need this? It always returns 0
        internal int MessageInspectorCorrelationOffset
        {
            get { return 0; }
        }

        internal int ParameterInspectorCorrelationOffset { get; }

        //        internal IRequestReplyCorrelator RequestReplyCorrelator
        //        {
        //            get { return this.requestReplyCorrelator; }
        //        }

        //        internal SecurityImpersonationBehavior SecurityImpersonation
        //        {
        //            get { return this.securityImpersonation; }
        //        }

        internal bool ValidateMustUnderstand { get; }

        internal ErrorBehavior ErrorBehavior
        {
            get { return error; }
        }

        bool AcquireDynamicInstanceContext(ref MessageRpc rpc)
        {
            //if (rpc.InstanceContext.QuotaThrottle != null)
            //{
            //    return AcquireDynamicInstanceContextCore(ref rpc);
            //}
            //else
            //{
                return true;
            //}
        }

        //        bool AcquireDynamicInstanceContextCore(ref MessageRpc rpc)
        //        {
        //            bool success = rpc.InstanceContext.QuotaThrottle.Acquire(rpc.Pause());

        //            if (success)
        //            {
        //                rpc.UnPause();
        //            }

        //            return success;
        //        }

        internal void AfterReceiveRequest(ref MessageRpc rpc)
        {
            if (messageInspectors.Length > 0)
            {
                AfterReceiveRequestCore(ref rpc);
            }
        }

        internal void AfterReceiveRequestCore(ref MessageRpc rpc)
        {
            int offset = MessageInspectorCorrelationOffset;
            try
            {
                for (int i = 0; i < messageInspectors.Length; i++)
                {
                    rpc.Correlation[offset + i] = messageInspectors[i].AfterReceiveRequest(ref rpc.Request, (IClientChannel)rpc.Channel.Proxy, rpc.InstanceContext);
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

        void BeforeSendReply(ref MessageRpc rpc, ref Exception exception, ref bool thereIsAnUnhandledException)
        {
            if (messageInspectors.Length > 0)
            {
                BeforeSendReplyCore(ref rpc, ref exception, ref thereIsAnUnhandledException);
            }
        }

        internal void BeforeSendReplyCore(ref MessageRpc rpc, ref Exception exception, ref bool thereIsAnUnhandledException)
        {
            int offset = MessageInspectorCorrelationOffset;
            for (int i = 0; i < messageInspectors.Length; i++)
            {
                try
                {
                    Message originalReply = rpc.Reply;
                    Message reply = originalReply;

                    messageInspectors[i].BeforeSendReply(ref reply, rpc.Correlation[offset + i]);
                    //if (TD.MessageInspectorBeforeSendInvokedIsEnabled())
                    //{
                    //    TD.MessageInspectorBeforeSendInvoked(rpc.EventTraceActivity, this.messageInspectors[i].GetType().FullName);
                    //}

                    if ((reply == null) && (originalReply != null))
                    {
                        string message = SR.Format(SR.SFxNullReplyFromExtension2, messageInspectors[i].GetType().ToString(), (rpc.Operation.Name ?? ""));
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
                    thereIsAnUnhandledException = (!error.HandleError(e)) || thereIsAnUnhandledException;
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
                error.HandleError(e);
            }
            catch (TimeoutException e)
            {
                error.HandleError(e);
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

                if (!error.HandleError(e))
                {
                    rpc.RequestContextThrewOnReply = true;
                    rpc.CanSendReply = false;
                }
            }

            return rpc;
        }

        void Reply(ref MessageRpc rpc)
        {
            rpc.RequestContextThrewOnReply = true;
            rpc.SuccessfullySendReply = false;

            try
            {
                rpc.RequestContext.ReplyAsync(rpc.Reply, rpc.ReplyTimeoutHelper.GetCancellationToken()).GetAwaiter().GetResult();
                rpc.RequestContextThrewOnReply = false;
                rpc.SuccessfullySendReply = true;

                //if (TD.DispatchMessageStopIsEnabled())
                //{
                //    TD.DispatchMessageStop(rpc.EventTraceActivity);
                //}
            }
            catch (CommunicationException e)
            {
                error.HandleError(e);
            }
            catch (TimeoutException e)
            {
                error.HandleError(e);
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

                if (!error.HandleError(e))
                {
                    rpc.RequestContextThrewOnReply = true;
                    rpc.CanSendReply = false;
                }
            }
        }

        internal Task<MessageRpc> DispatchAsync(ref MessageRpc rpc, bool isOperationContextSet)
        {
            rpc.ErrorProcessor = processMessageNonCleanupError;
            rpc.AsyncProcessor = ProcessMessageAsync;
            return rpc.ProcessAsync(isOperationContextSet);
        }

        //        void EndFinalizeCorrelation(ref MessageRpc rpc)
        //        {
        //            try
        //            {
        //                rpc.Reply = rpc.CorrelationCallback.EndFinalizeCorrelation(rpc.AsyncResult);
        //            }
        //            catch (Exception e)
        //            {
        //                if (Fx.IsFatal(e))
        //                {
        //                    throw;
        //                }

        //                if (!this.error.HandleError(e))
        //                {
        //                    rpc.CanSendReply = false;
        //                }
        //            }
        //        }

        bool EndReply(ref MessageRpc rpc)
        {
            bool success = false;

            try
            {
                rpc.TaskResult.GetAwaiter().GetResult();
                rpc.RequestContextThrewOnReply = false;
                success = true;

                //if (TD.DispatchMessageStopIsEnabled())
                //{
                //    TD.DispatchMessageStop(rpc.EventTraceActivity);
                //}
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                error.HandleError(e);
            }

            return success;
        }

        internal void InputSessionDoneReceiving(ServiceChannel channel)
        {
            if (inputSessionShutdownHandlers.Length > 0)
            {
                InputSessionDoneReceivingCore(channel);
            }
        }

        void InputSessionDoneReceivingCore(ServiceChannel channel)
        {
            IDuplexContextChannel proxy = channel.Proxy as IDuplexContextChannel;

            if (proxy != null)
            {
                IInputSessionShutdown[] handlers = inputSessionShutdownHandlers;
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
                    if (!error.HandleError(e))
                    {
                        proxy.Abort();
                    }
                }
            }
        }

        internal bool IsConcurrent(ref MessageRpc rpc)
        {
            return concurrency.IsConcurrent(ref rpc);
        }

        internal void InputSessionFaulted(ServiceChannel channel)
        {
            if (inputSessionShutdownHandlers.Length > 0)
            {
                InputSessionFaultedCore(channel);
            }
        }

        void InputSessionFaultedCore(ServiceChannel channel)
        {
            IDuplexContextChannel proxy = channel.Proxy as IDuplexContextChannel;

            if (proxy != null)
            {
                IInputSessionShutdown[] handlers = inputSessionShutdownHandlers;
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
                    if (!error.HandleError(e))
                    {
                        proxy.Abort();
                    }
                }
            }
        }

        //        static internal void GotDynamicInstanceContext(object state)
        //        {
        //            bool alreadyResumedNoLock;
        //            ((IResumeMessageRpc)state).Resume(out alreadyResumedNoLock);

        //            if (alreadyResumedNoLock)
        //            {
        //                Fx.Assert("GotDynamicInstanceContext more than once for same call.");
        //            }
        //        }

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

        //        static void OnFinalizeCorrelationCompletedCallback(IAsyncResult result)
        //        {
        //            if (result.CompletedSynchronously)
        //            {
        //                return;
        //            }

        //            IResumeMessageRpc resume = result.AsyncState as IResumeMessageRpc;

        //            if (resume == null)
        //            {
        //                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.SFxInvalidAsyncResultState0));
        //            }

        //            resume.Resume(result);
        //        }

        static void OnReplyCompletedCallback(Task result, object state)
        {
            IResumeMessageRpc resume = state as IResumeMessageRpc;

            if (resume == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.SFxInvalidAsyncResultState0);
            }

            resume.Resume(result);
        }

        void PrepareReply(ref MessageRpc rpc)
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
                        thereIsAnUnhandledException = (!error.HandleError(e)) || thereIsAnUnhandledException;
                        exception = e;
                    }
                }
            }

            BeforeSendReply(ref rpc, ref exception, ref thereIsAnUnhandledException);

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
                    error.ProvideOnlyFaultOfLastResort(ref rpc);

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
                        error.HandleError(e);
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

            if (!manualAddressing)
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
            return demuxer.GetOperation(ref message);
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

                if (!manualAddressing)
                {
                    EndpointAddress replyTo = rpc.ReplyToInfo.ReplyTo;
                    if (replyTo != null && replyTo.IsNone && rpc.Channel.IsReplyChannel)
                    {
                        CommunicationException error = new CommunicationException(SR.SFxRequestReplyNone);
                        throw TraceUtility.ThrowHelperError(error, rpc.Request);
                    }

                    if (isOnServer)
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

            if (concurrency.IsConcurrent(ref rpc))
            {
                rpc.Channel.IncrementActivity();
                rpc.SuccessfullyIncrementedActivity = true;
            }

            // TODO: Make authenticationBehavior Async
            if (authenticationBehavior != null)
            {
                authenticationBehavior.Authenticate(ref rpc);
            }

            // TODO: Make authorizationBehavior Async
            if (authorizationBehavior != null)
            {
                authorizationBehavior.Authorize(ref rpc);
            }

            instance.EnsureInstanceContext(ref rpc);
            // TODO: Work out what function the pending list has and re-add/replace/remove functionality
            //TransferChannelFromPendingList(ref rpc);

            AcquireDynamicInstanceContext(ref rpc);

            AfterReceiveRequest(ref rpc);

            rpc = await concurrency.LockInstanceAsync(rpc);
            rpc.SuccessfullyLockedInstance = true;

            try
            {
                // TaskHelpers has an extension method which enables awaitting a sync context to run continuation on it.
                await thread.GetSyncContext(rpc);
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
            // Note: for IManualConcurrencyOperationInvoker, the invoke assumes full control over pumping.
            // TODO: This is the concurrency gate. If the service is concurrent, this allows another receive to happen. This mechanism needs replacing.

            if (concurrency.IsConcurrent(ref rpc))
            {
                rpc.EnsureReceive();
            }

            instance.EnsureServiceInstance(ref rpc);

            try
            {
                //if (!rpc.Operation.IsSynchronous)
                //{
                //    // If async call completes in sync, it tells us through the gate below
                //    rpc.PrepareInvokeContinueGate();
                //}

                //if (this.transaction != null)
                //{
                //    this.transaction.InitializeCallContext(ref rpc);
                //}

                //SetActivityIdOnThread(ref rpc);

                rpc = await rpc.Operation.InvokeAsync(rpc);
            }
            catch
            {
                // This catch clause forces ClearCallContext to run prior to stackwalks exiting this frame.
                throw;
            }

            try
            {
                // Switch back to thread pool if we're using a non-default Sync Context
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
                error.ProvideMessageFault(ref rpc);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                error.HandleError(e);
            }

            PrepareReply(ref rpc);

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
                !object.ReferenceEquals(rpc.ErrorProcessor, processMessageCleanupError),
                "ProcessMessageCleanup run twice on the same MessageRpc!");
            rpc.ErrorProcessor = processMessageCleanupError;

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
                    error.HandleError(e);
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
                        error.HandleError(e);
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
                        error.HandleError(e);
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

                instance.AfterReply(ref rpc, error);

                if (rpc.SuccessfullyLockedInstance)
                {
                    try
                    {
                        concurrency.UnlockInstance(ref rpc);
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }

                        Fx.Assert("Exceptions should be caught by callee");
                        rpc.InstanceContext.FaultInternal();
                        error.HandleError(e);
                    }
                }

                if (terminate != null)
                {
                    try
                    {
                        terminate.AfterReply(ref rpc);
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }
                        error.HandleError(e);
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
                        error.HandleError(e);
                    }
                }
            }
            finally
            {
                //if (rpc.MessageRpcOwnsInstanceContextThrottle && rpc.channelHandler.InstanceContextServiceThrottle != null)
                //{
                //    rpc.channelHandler.InstanceContextServiceThrottle.DeactivateInstanceContext();
                //}

                //if (rpc.Activity != null && DiagnosticUtility.ShouldUseActivity)
                //{
                //    rpc.Activity.Stop();
                //}
            }

            error.HandleError(ref rpc);
            return rpc;
        }

        void ProcessMessageNonCleanupError(ref MessageRpc rpc)
        {
            try
            {
                error.ProvideMessageFault(ref rpc);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                error.HandleError(e);
            }

            PrepareReply(ref rpc);
        }

        void ProcessMessageCleanupError(ref MessageRpc rpc)
        {
            error.HandleError(ref rpc);
        }

        //        void ResolveTransactionOutcome(ref MessageRpc rpc)
        //        {
        //            if (this.transaction != null)
        //            {
        //                try
        //                {
        //                    bool hadError = (rpc.Error != null);
        //                    try
        //                    {
        //                        this.transaction.ResolveOutcome(ref rpc);
        //                    }
        //                    catch (FaultException e)
        //                    {
        //                        if (rpc.Error == null)
        //                        {
        //                            rpc.Error = e;
        //                        }
        //                    }
        //                    finally
        //                    {
        //                        if (!hadError && rpc.Error != null)
        //                        {
        //                            this.error.ProvideMessageFault(ref rpc);
        //                            this.PrepareAndAddressReply(ref rpc);
        //                        }
        //                    }
        //                }
        //                catch (Exception e)
        //                {
        //                    if (Fx.IsFatal(e))
        //                    {
        //                        throw;
        //                    }
        //                    this.error.HandleError(e);
        //                }

        //            }
        //        }

        //        [Fx.Tag.SecurityNote(Critical = "Calls security critical method to set the ActivityId on the thread",
        //            Safe = "Set the ActivityId only when MessageRpc is available")]
        //        [SecuritySafeCritical]
        //        void SetActivityIdOnThread(ref MessageRpc rpc)
        //        {
        //            if (FxTrace.Trace.IsEnd2EndActivityTracingEnabled && rpc.EventTraceActivity != null)
        //            {
        //                // Propagate the ActivityId to the service operation
        //                EventTraceActivityHelper.SetOnThread(rpc.EventTraceActivity);
        //            }
        //        }

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

        //        class CustomDemuxer : IDemuxer
        //        {
        //            Dictionary<string, DispatchOperationRuntime> map;
        //            IDispatchOperationSelector selector;
        //            DispatchOperationRuntime unhandled;

        //            internal CustomDemuxer(IDispatchOperationSelector selector)
        //            {
        //                this.selector = selector;
        //                this.map = new Dictionary<string, DispatchOperationRuntime>();
        //            }

        //            internal void Add(string name, DispatchOperationRuntime operation)
        //            {
        //                this.map.Add(name, operation);
        //            }

        //            internal void SetUnhandled(DispatchOperationRuntime operation)
        //            {
        //                this.unhandled = operation;
        //            }

        //            public DispatchOperationRuntime GetOperation(ref Message request)
        //            {
        //                string operationName = this.selector.SelectOperation(ref request);
        //                DispatchOperationRuntime operation = null;
        //                if (this.map.TryGetValue(operationName, out operation))
        //                {
        //                    return operation;
        //                }
        //                else
        //                {
        //                    return this.unhandled;
        //                }
        //            }
        //        }
    }
}