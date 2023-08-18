// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Channels
{
    internal abstract class ReliableInputSessionChannel : InputQueueInputChannel, IInputSessionChannel
    {
        private DeliveryStrategy<Message> _deliveryStrategy;
        private ReliableServiceDispatcherBase<IInputSessionChannel> _serviceDispatcher;
        protected string perfCounterId;
        private readonly IServiceScope _serviceScope;

        protected ReliableInputSessionChannel(
            ReliableServiceDispatcherBase<IInputSessionChannel> serviceDispatcher,
            IServerReliableChannelBinder binder,
            FaultHelper faultHelper,
            UniqueId inputID)
            : base(serviceDispatcher, serviceDispatcher, binder.LocalAddress)
        {
            Binder = binder;
            _serviceDispatcher = serviceDispatcher;
            Connection = new ReliableInputConnection();
            Connection.ReliableMessagingVersion = serviceDispatcher.ReliableMessagingVersion;
            ServerReliableSession = new ServerReliableSession(this, serviceDispatcher, binder, faultHelper, inputID, null);
            ServerReliableSession.UnblockChannelCloseCallback = UnblockClose;

            if (serviceDispatcher.Ordered)
                _deliveryStrategy = new OrderedDeliveryStrategy<Message>(this, serviceDispatcher.MaxTransferWindowSize, false);
            else
                _deliveryStrategy = new UnorderedDeliveryStrategy<Message>(this, serviceDispatcher.MaxTransferWindowSize);

            Binder.Faulted += OnBinderFaulted;
            Binder.OnException += OnBinderException;
            var sessionOpenTask = ServerReliableSession.OpenAsync(default);
            Fx.Assert(sessionOpenTask.IsCompleted, "OpenAsync should be completed synchronously");
            sessionOpenTask.GetAwaiter().GetResult();

            var serviceScopeFactory = Binder.Channel.GetProperty<IServiceScopeFactory>();
            _serviceScope = serviceScopeFactory.CreateScope();
            //if (PerformanceCounters.PerformanceCountersEnabled)
            //    perfCounterId = _serviceDispatcher.Uri.ToString().ToUpperInvariant();
        }

        protected bool AdvertisedZero { get; set; } = false;
        public IServerReliableChannelBinder Binder { get; }
        protected ReliableInputConnection Connection { get; }
        protected DeliveryStrategy<Message> DeliveryStrategy => _deliveryStrategy;
        protected ReliableServiceDispatcherBase<IInputSessionChannel> ServiceDispatcher => _serviceDispatcher;
        protected ChannelReliableSession ReliableSession => ServerReliableSession;
        public IInputSession Session => ServerReliableSession;
        internal ServerReliableSession ServerReliableSession { get; set; }

        protected virtual void AbortGuards() { }

        protected void AddAcknowledgementHeader(Message message)
        {
            int bufferRemaining = -1;

            if (ServiceDispatcher.FlowControlEnabled)
            {
                bufferRemaining = ServiceDispatcher.MaxTransferWindowSize - _deliveryStrategy.EnqueuedCount;
                AdvertisedZero = (bufferRemaining == 0);
            }

            WsrmUtilities.AddAcknowledgementHeader(_serviceDispatcher.ReliableMessagingVersion, message,
                ServerReliableSession.InputID, Connection.Ranges, Connection.IsLastKnown, bufferRemaining);
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            Binder.Faulted -= OnBinderFaulted;
            _deliveryStrategy.Dispose();
        }

        protected virtual Task CloseGuardsAsync(CancellationToken token) => Task.CompletedTask;

        protected Message CreateAcknowledgmentMessage()
        {
            int bufferRemaining = -1;

            if (ServiceDispatcher.FlowControlEnabled)
            {
                bufferRemaining = ServiceDispatcher.MaxTransferWindowSize - _deliveryStrategy.EnqueuedCount;
                AdvertisedZero = (bufferRemaining == 0);
            }

            Message message = WsrmUtilities.CreateAcknowledgmentMessage(
                _serviceDispatcher.MessageVersion,
                _serviceDispatcher.ReliableMessagingVersion,
                ServerReliableSession.InputID,
                Connection.Ranges,
                Connection.IsLastKnown,
                bufferRemaining);

            return message;
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IInputSessionChannel))
            {
                return (T)(object)this;
            }

            T baseProperty = base.GetProperty<T>();
            if (baseProperty != null)
            {
                return baseProperty;
            }

            T scopedProperty = _serviceScope.ServiceProvider.GetService<T>();
            if (scopedProperty != null)
            {
                return scopedProperty;
            }

            T innerProperty = Binder.Channel.GetProperty<T>();
            if ((innerProperty == null) && (typeof(T) == typeof(FaultConverter)))
            {
                return (T)(object)FaultConverter.GetDefaultFaultConverter(_serviceDispatcher.MessageVersion);
            }
            else
            {
                return innerProperty;
            }
        }

        protected override void OnAbort()
        {
            Connection.Abort(this);
            AbortGuards();
            ServerReliableSession.Abort();
            _serviceDispatcher.OnReliableChannelAbort(ReliableSession.InputID, null);
            base.OnAbort();
        }

        private void OnBinderException(IReliableChannelBinder sender, Exception exception)
        {
            if (exception is QuotaExceededException)
                ServerReliableSession.OnLocalFault(exception, SequenceTerminatedFault.CreateQuotaExceededFault(ServerReliableSession.OutputID), null);
            else
                Enqueue(exception, null);
        }

        private void OnBinderFaulted(IReliableChannelBinder sender, Exception exception)
        {
            Binder.Abort();
            exception = new CommunicationException(SR.EarlySecurityFaulted, exception);
            ServerReliableSession.OnLocalFault(exception, (Message)null, null);
        }

        protected override async Task OnCloseAsync(CancellationToken  token)
        {
            ThrowIfCloseInvalid();

            await Connection.CloseAsync(token);
            await ServerReliableSession.CloseAsync(token);
            await CloseGuardsAsync(token);
            await Binder.CloseAsync(token, MaskingMode.Handled);
            await _serviceDispatcher.OnReliableChannelCloseAsync(ReliableSession.InputID, null, token);
            _serviceScope.Dispose();
            await base.OnCloseAsync(token);
        }

        protected override void OnFaulted()
        {
            ServerReliableSession.OnFaulted();
            UnblockClose();
            base.OnFaulted();
            //if (PerformanceCounters.PerformanceCountersEnabled)
            //    PerformanceCounters.SessionFaulted(perfCounterId);
        }

        protected virtual void OnQuotaAvailable()
        {
        }

        private void ThrowIfCloseInvalid()
        {
            bool shouldFault = false;

            if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                if (DeliveryStrategy.EnqueuedCount > 0 || Connection.Ranges.Count > 1)
                {
                    shouldFault = true;
                }
            }
            else if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                if (DeliveryStrategy.EnqueuedCount > 0)
                {
                    shouldFault = true;
                }
            }

            if (shouldFault)
            {
                WsrmFault fault = SequenceTerminatedFault.CreateProtocolFault(ServerReliableSession.InputID,
                    SR.SequenceTerminatedSessionClosedBeforeDone, SR.SessionClosedBeforeDone);
                ServerReliableSession.OnLocalFault(null, fault, null);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(fault.CreateException());
            }
        }

        private void UnblockClose()
        {
            Connection.Fault(this);
        }
    }

    internal sealed class ReliableInputSessionChannelOverDuplex : ReliableInputSessionChannel
    {
        private TimeSpan acknowledgementInterval;
        private bool acknowledgementScheduled = false;
        private readonly IOThreadTimer acknowledgementTimer;
        private readonly Guard guard = new Guard(int.MaxValue);
        private int pendingAcknowledgements = 0;

        public ReliableInputSessionChannelOverDuplex(
            ReliableServiceDispatcherBase<IInputSessionChannel> serviceDispatcher,
            IServerReliableChannelBinder binder, FaultHelper faultHelper,
            UniqueId inputID)
            : base(serviceDispatcher, binder, faultHelper, inputID)
        {
            acknowledgementInterval = serviceDispatcher.AcknowledgementInterval;
            acknowledgementTimer = new IOThreadTimer(new Action<object>(OnAcknowledgementTimeoutElapsed), null, true);
            DeliveryStrategy.DequeueCallback = OnDeliveryStrategyItemDequeued;
        }

        protected override void AbortGuards()
        {
            guard.Abort();
        }

        protected override Task CloseGuardsAsync(CancellationToken token) => guard.CloseAsync(token);

        public override async Task DispatchAsync(Message message)
        {
            WsrmMessageInfo info = WsrmMessageInfo.Get(ServiceDispatcher.MessageVersion,
                ServiceDispatcher.ReliableMessagingVersion, Binder.Channel, Binder.GetInnerSession(),
                message);

            await ProcessMessageAsync(info);
        }

        // Based on HandleReceiveComplete
        public override async Task DispatchAsync(RequestContext context)
        {
            if (context == null)
            {
                bool terminated = false;

                lock (ThisLock)
                {
                    terminated = Connection.Terminate();
                }

                if (!terminated && (Binder.State == CommunicationState.Opened))
                {
                    Exception e = new CommunicationException(SR.EarlySecurityClose);
                    ReliableSession.OnLocalFault(e, (Message)null, null);
                }

                return;
            }

            Message message = context.RequestMessage;
            await context.CloseAsync();

            WsrmMessageInfo info = WsrmMessageInfo.Get(ServiceDispatcher.MessageVersion,
                ServiceDispatcher.ReliableMessagingVersion, Binder.Channel, Binder.GetInnerSession(),
                message);

            await ProcessMessageAsync(info);
        }

        private void OnAcknowledgementTimeoutElapsed(object state)
        {
            lock (ThisLock)
            {
                acknowledgementScheduled = false;
                pendingAcknowledgements = 0;

                if (State == CommunicationState.Closing
                    || State == CommunicationState.Closed
                    || State == CommunicationState.Faulted)
                    return;
            }

            _ = OnAcknowledgementTimeoutElapsedAsync();
        }

        private async Task OnAcknowledgementTimeoutElapsedAsync()
        {
            if (await guard.EnterAsync())
            {
                try
                {
                    using (Message message = CreateAcknowledgmentMessage())
                    {
                        await Binder.SendAsync(message, TimeoutHelper.GetCancellationToken(DefaultSendTimeout));
                    }
                }

                finally
                {
                    guard.Exit();
                }
            }
        }

        private void OnDeliveryStrategyItemDequeued()
        {
            if (AdvertisedZero)
                OnAcknowledgementTimeoutElapsed(null);
        }

        protected override void OnClosing()
        {
            base.OnClosing();
            acknowledgementTimer.Cancel();
        }

        protected override void OnQuotaAvailable()
        {
            OnAcknowledgementTimeoutElapsed(null);
        }

        public async Task ProcessDemuxedMessageAsync(WsrmMessageInfo info)
        {
            try
            {
                await ProcessMessageAsync(info);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                    throw;

                ReliableSession.OnUnknownException(e);
            }
        }

        private async Task ProcessMessageAsync(WsrmMessageInfo info)
        {
            bool closeMessage = true;

            try
            {
                if (!await ReliableSession.ProcessInfoAsync(info, null))
                {
                    closeMessage = false;
                    return;
                }

                if (!ReliableSession.VerifySimplexProtocolElements(info, null))
                {
                    closeMessage = false;
                    return;
                }

                ReliableSession.OnRemoteActivity(false);

                if (info.CreateSequenceInfo != null)
                {
                    EndpointAddress acksTo;

                    if (WsrmUtilities.ValidateCreateSequence(info, ServiceDispatcher, Binder.Channel, out acksTo))
                    {
                        Message response = WsrmUtilities.CreateCreateSequenceResponse(ServiceDispatcher.MessageVersion,
                            ServiceDispatcher.ReliableMessagingVersion, false, info.CreateSequenceInfo,
                            ServiceDispatcher.Ordered, ReliableSession.InputID, acksTo);

                        using (response)
                        {
                            if (Binder.AddressResponse(info.Message, response))
                            {
                                await Binder.SendAsync(response, TimeoutHelper.GetCancellationToken(DefaultSendTimeout));
                            }
                        }
                    }
                    else
                    {
                        ReliableSession.OnLocalFault(info.FaultException, info.FaultReply, null);
                    }

                    return;
                }

                bool scheduleShutdown = false;
                bool tryAckNow = (info.AckRequestedInfo != null);
                bool terminate = false;
                Message message = null;
                WsrmFault fault = null;
                Exception remoteFaultException = null;
                bool wsrmFeb2005 = ServiceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005;
                bool wsrm11 = ServiceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11;

                if (info.SequencedMessageInfo != null)
                {
                    lock (ThisLock)
                    {
                        if (Aborted || State == CommunicationState.Faulted)
                        {
                            return;
                        }

                        long sequenceNumber = info.SequencedMessageInfo.SequenceNumber;
                        bool isLast = wsrmFeb2005 && info.SequencedMessageInfo.LastMessage;

                        if (!Connection.IsValid(sequenceNumber, isLast))
                        {
                            if (wsrmFeb2005)
                            {
                                fault = new LastMessageNumberExceededFault(ReliableSession.InputID);
                            }
                            else
                            {
                                message = new SequenceClosedFault(ReliableSession.InputID).CreateMessage(
                                    ServiceDispatcher.MessageVersion, ServiceDispatcher.ReliableMessagingVersion);
                                tryAckNow = true;

                                //if (PerformanceCounters.PerformanceCountersEnabled)
                                //    PerformanceCounters.MessageDropped(perfCounterId);
                            }
                        }
                        else if (Connection.Ranges.Contains(sequenceNumber))
                        {
                            //if (PerformanceCounters.PerformanceCountersEnabled)
                            //    PerformanceCounters.MessageDropped(perfCounterId);

                            tryAckNow = true;
                        }
                        else if (wsrmFeb2005 && info.Action == WsrmFeb2005Strings.LastMessageAction)
                        {
                            Connection.Merge(sequenceNumber, isLast);

                            if (Connection.AllAdded)
                            {
                                scheduleShutdown = true;
                                ReliableSession.CloseSession();
                            }
                        }
                        else if (State == CommunicationState.Closing)
                        {
                            if (wsrmFeb2005)
                            {
                                fault = SequenceTerminatedFault.CreateProtocolFault(ReliableSession.InputID,
                                    SR.SequenceTerminatedSessionClosedBeforeDone,
                                    SR.SessionClosedBeforeDone);
                            }
                            else
                            {
                                message = new SequenceClosedFault(ReliableSession.InputID).CreateMessage(
                                    ServiceDispatcher.MessageVersion, ServiceDispatcher.ReliableMessagingVersion);
                                tryAckNow = true;

                                //if (PerformanceCounters.PerformanceCountersEnabled)
                                //    PerformanceCounters.MessageDropped(perfCounterId);
                            }
                        }
                        // In the unordered case we accept no more than MaxSequenceRanges ranges to limit the
                        // serialized ack size and the amount of memory taken by the ack ranges. In the
                        // ordered case, the delivery strategy MaxTransferWindowSize quota mitigates this
                        // threat.
                        else if (DeliveryStrategy.CanEnqueue(sequenceNumber)
                            && (ServiceDispatcher.Ordered || Connection.CanMerge(sequenceNumber)))
                        {
                            Connection.Merge(sequenceNumber, isLast);
                            DeliveryStrategy.Enqueue(info.Message, sequenceNumber);
                            closeMessage = false;

                            pendingAcknowledgements++;
                            if (pendingAcknowledgements == ServiceDispatcher.MaxTransferWindowSize)
                                tryAckNow = true;

                            if (Connection.AllAdded)
                            {
                                scheduleShutdown = true;
                                ReliableSession.CloseSession();
                            }
                        }
                        else
                        {
                            //if (PerformanceCounters.PerformanceCountersEnabled)
                            //    PerformanceCounters.MessageDropped(perfCounterId);
                        }

                        if (Connection.IsLastKnown)
                            tryAckNow = true;

                        if (!tryAckNow && pendingAcknowledgements > 0 && !acknowledgementScheduled && fault == null)
                        {
                            acknowledgementScheduled = true;
                            acknowledgementTimer.Set(acknowledgementInterval);
                        }
                    }
                }
                else if (wsrmFeb2005 && info.TerminateSequenceInfo != null)
                {
                    bool isTerminateEarly;

                    lock (ThisLock)
                    {
                        isTerminateEarly = !Connection.Terminate();
                    }

                    if (isTerminateEarly)
                    {
                        fault = SequenceTerminatedFault.CreateProtocolFault(ReliableSession.InputID,
                            SR.SequenceTerminatedEarlyTerminateSequence,
                            SR.EarlyTerminateSequence);
                    }
                }
                else if (wsrm11 && ((info.TerminateSequenceInfo != null) || info.CloseSequenceInfo != null))
                {
                    bool isTerminate = info.TerminateSequenceInfo != null;
                    WsrmRequestInfo requestInfo = isTerminate
                        ? info.TerminateSequenceInfo
                        : info.CloseSequenceInfo;
                    long last = isTerminate ? info.TerminateSequenceInfo.LastMsgNumber : info.CloseSequenceInfo.LastMsgNumber;

                    if (!WsrmUtilities.ValidateWsrmRequest(ReliableSession, requestInfo, Binder, null))
                    {
                        return;
                    }

                    bool isLastLargeEnough = true;
                    bool isLastConsistent = true;

                    lock (ThisLock)
                    {
                        if (!Connection.IsLastKnown)
                        {
                            if (isTerminate)
                            {
                                if (Connection.SetTerminateSequenceLast(last, out isLastLargeEnough))
                                {
                                    scheduleShutdown = true;
                                }
                                else if (isLastLargeEnough)
                                {
                                    remoteFaultException = new ProtocolException(SR.EarlyTerminateSequence);
                                }
                            }
                            else
                            {
                                scheduleShutdown = Connection.SetCloseSequenceLast(last);
                                isLastLargeEnough = scheduleShutdown;
                            }

                            if (scheduleShutdown)
                            {
                                ReliableSession.SetFinalAck(Connection.Ranges);
                                DeliveryStrategy.Dispose();
                            }
                        }
                        else
                        {
                            isLastConsistent = (last == Connection.Last);

                            // Have seen CloseSequence already, TerminateSequence means cleanup.
                            if (isTerminate && isLastConsistent && Connection.IsSequenceClosed)
                            {
                                terminate = true;
                            }
                        }
                    }

                    if (!isLastLargeEnough)
                    {
                        fault = SequenceTerminatedFault.CreateProtocolFault(ReliableSession.InputID,
                            SR.SequenceTerminatedSmallLastMsgNumber,
                            SR.SmallLastMsgNumberExceptionString);
                    }
                    else if (!isLastConsistent)
                    {
                        fault = SequenceTerminatedFault.CreateProtocolFault(ReliableSession.InputID,
                            SR.SequenceTerminatedInconsistentLastMsgNumber,
                            SR.InconsistentLastMsgNumberExceptionString);
                    }
                    else
                    {
                        message = isTerminate
                            ? WsrmUtilities.CreateTerminateResponseMessage(ServiceDispatcher.MessageVersion,
                            requestInfo.MessageId, ReliableSession.InputID)
                            : WsrmUtilities.CreateCloseSequenceResponse(ServiceDispatcher.MessageVersion,
                            requestInfo.MessageId, ReliableSession.InputID);

                        tryAckNow = true;
                    }
                }

                if (fault != null)
                {
                    ReliableSession.OnLocalFault(fault.CreateException(), fault, null);
                }
                else
                {
                    if (tryAckNow)
                    {
                        lock (ThisLock)
                        {
                            if (acknowledgementScheduled)
                            {
                                acknowledgementTimer.Cancel();
                                acknowledgementScheduled = false;
                            }

                            pendingAcknowledgements = 0;
                        }

                        if (message != null)
                        {
                            AddAcknowledgementHeader(message);
                        }
                        else
                        {
                            message = CreateAcknowledgmentMessage();
                        }
                    }

                    if (message != null)
                    {
                        using (message)
                        {
                            if (await guard.EnterAsync())
                            {
                                try
                                {
                                    await Binder.SendAsync(message, TimeoutHelper.GetCancellationToken(DefaultSendTimeout));
                                }
                                finally
                                {
                                    guard.Exit();
                                }
                            }
                        }
                    }

                    if (terminate)
                    {
                        lock (ThisLock)
                        {
                            Connection.Terminate();
                        }
                    }

                    if (remoteFaultException != null)
                    {
                        ReliableSession.OnRemoteFault(remoteFaultException);
                        return;
                    }
                }
            }
            finally
            {
                if (closeMessage)
                {
                    info.Message.Close();
                }
            }
        }
    }

    internal sealed class ReliableInputSessionChannelOverReply : ReliableInputSessionChannel
    {
        public ReliableInputSessionChannelOverReply(
            ReliableServiceDispatcherBase<IInputSessionChannel> serviceDispatcher,
            IServerReliableChannelBinder binder, FaultHelper faultHelper,
            UniqueId inputID)
            : base(serviceDispatcher, binder, faultHelper, inputID)
        {

        }

        public override Task DispatchAsync(Message message)
        {
            throw new NotImplementedException();
        }

        // Based on HandleReceiveComplete
        public override Task DispatchAsync(RequestContext context)
        {
            // TODO, resolve what to do with the missing information of timeoutOkay that was originally retruend from Binger.EndTryReceive
            if (context == null)
            {
                bool terminated = false;

                lock (ThisLock)
                {
                    terminated = Connection.Terminate();
                }

                if (!terminated && (Binder.State == CommunicationState.Opened))
                {
                    Exception e = new CommunicationException(SR.EarlySecurityClose);
                    ReliableSession.OnLocalFault(e, (Message)null, null);
                }
                return Task.CompletedTask;
            }

            WsrmMessageInfo info = WsrmMessageInfo.Get(ServiceDispatcher.MessageVersion,
                ServiceDispatcher.ReliableMessagingVersion, Binder.Channel, Binder.GetInnerSession(),
                context.RequestMessage);

            return ProcessRequestAsync(context, info);
        }

        public async Task ProcessDemuxedRequestAsync(RequestContext context, WsrmMessageInfo info)
        {
            try
            {
                await ProcessRequestAsync(context, info);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                    throw;

                ReliableSession.OnUnknownException(e);
            }
        }

        private async Task ProcessRequestAsync(RequestContext context, WsrmMessageInfo info)
        {
            bool closeContext = true;
            bool closeMessage = true;

            try
            {
                if (!await ReliableSession.ProcessInfoAsync(info, context))
                {
                    closeContext = false;
                    closeMessage = false;
                    return;
                }

                if (!ReliableSession.VerifySimplexProtocolElements(info, context))
                {
                    closeContext = false;
                    closeMessage = false;
                    return;
                }

                ReliableSession.OnRemoteActivity(false);

                if (info.CreateSequenceInfo != null)
                {
                    EndpointAddress acksTo;

                    if (WsrmUtilities.ValidateCreateSequence<IInputSessionChannel>(info, ServiceDispatcher, Binder.Channel, out acksTo))
                    {
                        Message response = WsrmUtilities.CreateCreateSequenceResponse(ServiceDispatcher.MessageVersion,
                            ServiceDispatcher.ReliableMessagingVersion, false, info.CreateSequenceInfo,
                            ServiceDispatcher.Ordered, ReliableSession.InputID, acksTo);

                        using (context)
                        {
                            using (response)
                            {
                                if (Binder.AddressResponse(info.Message, response))
                                {
                                    await context.ReplyAsync(response, TimeoutHelper.GetCancellationToken(DefaultSendTimeout));
                                }
                            }
                        }
                    }
                    else
                    {
                        ReliableSession.OnLocalFault(info.FaultException, info.FaultReply, context);
                    }

                    closeContext = false;
                    closeMessage = false;
                    return;
                }

                bool scheduleShutdown = false;
                bool terminate = false;
                WsrmFault fault = null;
                Message message = null;
                Exception remoteFaultException = null;
                bool wsrmFeb2005 = ServiceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005;
                bool wsrm11 = ServiceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11;
                bool addAck = info.AckRequestedInfo != null;

                if (info.SequencedMessageInfo != null)
                {
                    lock (ThisLock)
                    {
                        if (Aborted || (State == CommunicationState.Faulted))
                        {
                            return;
                        }
                        long sequenceNumber = info.SequencedMessageInfo.SequenceNumber;
                        bool isLast = wsrmFeb2005 && info.SequencedMessageInfo.LastMessage;

                        if (!Connection.IsValid(sequenceNumber, isLast))
                        {
                            if (wsrmFeb2005)
                            {
                                fault = new LastMessageNumberExceededFault(ReliableSession.InputID);
                            }
                            else
                            {
                                message = new SequenceClosedFault(ReliableSession.InputID).CreateMessage(
                                    ServiceDispatcher.MessageVersion, ServiceDispatcher.ReliableMessagingVersion);

                                //if (PerformanceCounters.PerformanceCountersEnabled)
                                //    PerformanceCounters.MessageDropped(perfCounterId);
                            }
                        }
                        else if (Connection.Ranges.Contains(sequenceNumber))
                        {
                            //if (PerformanceCounters.PerformanceCountersEnabled)
                            //    PerformanceCounters.MessageDropped(perfCounterId);
                        }
                        else if (wsrmFeb2005 && info.Action == WsrmFeb2005Strings.LastMessageAction)
                        {
                            Connection.Merge(sequenceNumber, isLast);
                            scheduleShutdown = Connection.AllAdded;
                        }
                        else if (State == CommunicationState.Closing)
                        {
                            if (wsrmFeb2005)
                            {
                                fault = SequenceTerminatedFault.CreateProtocolFault(ReliableSession.InputID,
                                    SR.SequenceTerminatedSessionClosedBeforeDone,
                                    SR.SessionClosedBeforeDone);
                            }
                            else
                            {
                                message = new SequenceClosedFault(ReliableSession.InputID).CreateMessage(
                                    ServiceDispatcher.MessageVersion, ServiceDispatcher.ReliableMessagingVersion);

                                //if (PerformanceCounters.PerformanceCountersEnabled)
                                //    PerformanceCounters.MessageDropped(perfCounterId);
                            }
                        }
                        // In the unordered case we accept no more than MaxSequenceRanges ranges to limit the
                        // serialized ack size and the amount of memory taken by the ack ranges. In the
                        // ordered case, the delivery strategy MaxTransferWindowSize quota mitigates this
                        // threat.
                        else if (DeliveryStrategy.CanEnqueue(sequenceNumber)
                            && (ServiceDispatcher.Ordered || Connection.CanMerge(sequenceNumber)))
                        {
                            Connection.Merge(sequenceNumber, isLast);
                            DeliveryStrategy.Enqueue(info.Message, sequenceNumber);
                            scheduleShutdown = Connection.AllAdded;
                            closeMessage = false;
                        }
                        else
                        {
                            //if (PerformanceCounters.PerformanceCountersEnabled)
                            //    PerformanceCounters.MessageDropped(perfCounterId);
                        }
                    }
                }
                else if (wsrmFeb2005 && info.TerminateSequenceInfo != null)
                {
                    bool isTerminateEarly;

                    lock (ThisLock)
                    {
                        isTerminateEarly = !Connection.Terminate();
                    }

                    if (isTerminateEarly)
                    {
                        fault = SequenceTerminatedFault.CreateProtocolFault(ReliableSession.InputID,
                            SR.SequenceTerminatedEarlyTerminateSequence,
                            SR.EarlyTerminateSequence);
                    }
                    else
                    {
                        // In the normal case, TerminateSequence is a one-way operation, returning (the finally
                        // block will close the context).
                        return;
                    }
                }
                else if (wsrm11 && ((info.TerminateSequenceInfo != null) || (info.CloseSequenceInfo != null)))
                {
                    bool isTerminate = (info.TerminateSequenceInfo != null);
                    WsrmRequestInfo requestInfo = isTerminate
                        ? info.TerminateSequenceInfo
                        : info.CloseSequenceInfo;
                    long last = isTerminate ? info.TerminateSequenceInfo.LastMsgNumber : info.CloseSequenceInfo.LastMsgNumber;

                    if (!WsrmUtilities.ValidateWsrmRequest(ReliableSession, requestInfo, Binder, context))
                    {
                        closeMessage = false;
                        closeContext = false;
                        return;
                    }

                    bool isLastLargeEnough = true;
                    bool isLastConsistent = true;

                    lock (ThisLock)
                    {
                        if (!Connection.IsLastKnown)
                        {
                            if (isTerminate)
                            {
                                if (Connection.SetTerminateSequenceLast(last, out isLastLargeEnough))
                                {
                                    scheduleShutdown = true;
                                }
                                else if (isLastLargeEnough)
                                {
                                    remoteFaultException = new ProtocolException(SR.EarlyTerminateSequence);
                                }
                            }
                            else
                            {
                                scheduleShutdown = Connection.SetCloseSequenceLast(last);
                                isLastLargeEnough = scheduleShutdown;
                            }

                            if (scheduleShutdown)
                            {
                                ReliableSession.SetFinalAck(Connection.Ranges);
                                DeliveryStrategy.Dispose();
                            }
                        }
                        else
                        {
                            isLastConsistent = (last == Connection.Last);

                            // Have seen CloseSequence already, TerminateSequence means cleanup.
                            if (isTerminate && isLastConsistent && Connection.IsSequenceClosed)
                            {
                                terminate = true;
                            }
                        }
                    }

                    if (!isLastLargeEnough)
                    {
                        fault = SequenceTerminatedFault.CreateProtocolFault(ReliableSession.InputID,
                            SR.SequenceTerminatedSmallLastMsgNumber,
                            SR.SmallLastMsgNumberExceptionString);
                    }
                    else if (!isLastConsistent)
                    {
                        fault = SequenceTerminatedFault.CreateProtocolFault(ReliableSession.InputID,
                            SR.SequenceTerminatedInconsistentLastMsgNumber,
                            SR.InconsistentLastMsgNumberExceptionString);
                    }
                    else
                    {
                        message = isTerminate
                            ? WsrmUtilities.CreateTerminateResponseMessage(ServiceDispatcher.MessageVersion,
                            requestInfo.MessageId, ReliableSession.InputID)
                            : WsrmUtilities.CreateCloseSequenceResponse(ServiceDispatcher.MessageVersion,
                            requestInfo.MessageId, ReliableSession.InputID);
                        addAck = true;
                    }
                }

                if (fault != null)
                {
                    ReliableSession.OnLocalFault(fault.CreateException(), fault, context);
                    closeMessage = false;
                    closeContext = false;
                    return;
                }

                if (message != null && addAck)
                {
                    AddAcknowledgementHeader(message);
                }
                else if (message == null)
                {
                    message = CreateAcknowledgmentMessage();
                }

                using (message)
                {
                    await context.ReplyAsync(message);
                }

                if (terminate)
                {
                    lock (ThisLock)
                    {
                        Connection.Terminate();
                    }
                }

                if (remoteFaultException != null)
                {
                    ReliableSession.OnRemoteFault(remoteFaultException);
                    return;
                }
            }
            finally
            {
                if (closeMessage)
                    info.Message.Close();

                if (closeContext)
                    await context.CloseAsync();
            }
        }
    }
}
