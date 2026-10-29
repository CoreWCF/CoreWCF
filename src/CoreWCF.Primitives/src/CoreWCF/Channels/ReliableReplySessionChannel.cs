// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Channels
{
    // Note on locking:
    // The following rule must be followed in order to avoid deadlocks: ReliableRequestContext
    // locks MUST NOT be taken while under the ReliableReplySessionChannel lock.
    //
    // lock(context)-->lock(channel) ok.    
    // lock(channel)-->lock(context) BAD!
    //
    internal sealed class ReliableReplySessionChannel : InputQueueReplyChannel, IReplySessionChannel
    {
        private readonly List<long> _acked = new List<long>();
        private readonly IServerReliableChannelBinder _binder;
        private ReplyHelper _closeSequenceReplyHelper;
        private readonly ReliableInputConnection _connection;
        private bool _contextAborted;
        private readonly DeliveryStrategy<RequestContext> _deliveryStrategy;
        private ReliableRequestContext _lastReply;
        private bool _lastReplyAcked;
        private long _lastReplySequenceNumber = long.MinValue;
        private readonly ReliableServiceDispatcherBase<IReplySessionChannel> _serviceDispatcher;
        private readonly InterruptibleWaitObject _messagingCompleteWaitObject;
        private long _nextReplySequenceNumber;
        //private readonly string _perfCounterId;
        private readonly Dictionary<long, ReliableRequestContext> _requestsByRequestSequenceNumber = new Dictionary<long, ReliableRequestContext>();
        private readonly Dictionary<long, ReliableRequestContext> _requestsByReplySequenceNumber = new Dictionary<long, ReliableRequestContext>();
        private ServerReliableSession _session;
        private ReplyHelper _terminateSequenceReplyHelper;
        private readonly IServiceScope _serviceScope;

        public ReliableReplySessionChannel(
            ReliableServiceDispatcherBase<IReplySessionChannel> serviceDispatcher,
            IServerReliableChannelBinder binder,
            FaultHelper faultHelper,
            UniqueId inputID,
            UniqueId outputID)
            : base(serviceDispatcher, serviceDispatcher.InnerServiceDispatcher, binder.LocalAddress)
        {
            _serviceDispatcher = serviceDispatcher;
            _connection = new ReliableInputConnection();
            _connection.ReliableMessagingVersion = _serviceDispatcher.ReliableMessagingVersion;
            _binder = binder;
            _session = new ServerReliableSession(this, serviceDispatcher, binder, faultHelper, inputID, outputID);
            _session.UnblockChannelCloseCallback = this.UnblockClose;

            if (_serviceDispatcher.Ordered)
                _deliveryStrategy = new OrderedDeliveryStrategy<RequestContext>(this, _serviceDispatcher.MaxTransferWindowSize, true);
            else
                _deliveryStrategy = new UnorderedDeliveryStrategy<RequestContext>(this, _serviceDispatcher.MaxTransferWindowSize);
            _binder.Faulted += OnBinderFaulted;
            _binder.OnException += OnBinderException;
            if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                _messagingCompleteWaitObject = new InterruptibleWaitObject(false);
            }

            // ServerReliableSession.OpenAsync is implemented synchronously. If this changes, we need to refactor
            // so that we don't call it from the constructor.
            var sessionOpenTask = _session.OpenAsync(default);
            Fx.Assert(sessionOpenTask.IsCompleted, "ReliableReplySessionChannel: Session open task is not completed");
            sessionOpenTask.GetAwaiter().GetResult();

            var serviceScopeFactory = _binder.Channel.GetProperty<IServiceScopeFactory>();
            _serviceScope = serviceScopeFactory.CreateScope();

            //if (PerformanceCounters.PerformanceCountersEnabled)
            //    _perfCounterId = _listener.Uri.ToString().ToUpperInvariant();
        }

        public IServerReliableChannelBinder Binder => _binder;

        private bool IsMessagingCompleted
        {
            get
            {
                lock (ThisLock)
                {
                    return _connection.AllAdded && (_requestsByRequestSequenceNumber.Count == 0) && _lastReplyAcked;
                }
            }
        }

        private MessageVersion MessageVersion => _serviceDispatcher.MessageVersion;

        private int PendingRequestContexts
        {
            get
            {
                lock (ThisLock)
                {
                    return _requestsByRequestSequenceNumber.Count - _requestsByReplySequenceNumber.Count;
                }
            }
        }

        public IInputSession Session => _session;

        private void AbortContexts()
        {
            lock (ThisLock)
            {
                if (_contextAborted)
                    return;
                _contextAborted = true;
            }

            Dictionary<long, ReliableRequestContext>.ValueCollection contexts = _requestsByRequestSequenceNumber.Values;

            foreach (ReliableRequestContext request in contexts)
            {
                request.Abort();
            }

            _requestsByRequestSequenceNumber.Clear();
            _requestsByReplySequenceNumber.Clear();


            if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                if (_lastReply != null)
                    _lastReply.Abort();
            }
        }

        private void AddAcknowledgementHeader(Message message)
        {
            WsrmUtilities.AddAcknowledgementHeader(
                _serviceDispatcher.ReliableMessagingVersion,
                message,
                _session.InputID,
                _connection.Ranges,
                _connection.IsLastKnown,
                _serviceDispatcher.MaxTransferWindowSize - _deliveryStrategy.EnqueuedCount);
        }

        private Task CloseOutputAsync(CancellationToken token)
        {
            if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                ReliableRequestContext reply = _lastReply;
                if (reply == null)
                    return Task.CompletedTask;
                else
                    return reply.ReplyInternalAsync(null, token);
            }
            else
            {
                lock (ThisLock)
                {
                    ThrowIfClosed();
                    CreateCloseSequenceReplyHelper();
                }
                return _closeSequenceReplyHelper.WaitAndReplyAsync(token);
            }
        }

        private Message CreateAcknowledgement(SequenceRangeCollection ranges)
        {
            Message message = WsrmUtilities.CreateAcknowledgmentMessage(
                MessageVersion,
                _serviceDispatcher.ReliableMessagingVersion,
                _session.InputID,
                ranges,
                _connection.IsLastKnown,
                _serviceDispatcher.MaxTransferWindowSize - _deliveryStrategy.EnqueuedCount);

            return message;
        }

        private Message CreateSequenceClosedFault()
        {
            Message message = new SequenceClosedFault(_session.InputID).CreateMessage(
                _serviceDispatcher.MessageVersion, _serviceDispatcher.ReliableMessagingVersion);
            AddAcknowledgementHeader(message);
            return message;
        }

        private bool CreateCloseSequenceReplyHelper()
        {
            if (State == CommunicationState.Faulted || Aborted)
            {
                return false;
            }

            if (_closeSequenceReplyHelper == null)
            {
                _closeSequenceReplyHelper = new ReplyHelper(this, CloseSequenceReplyProvider.Instance, true);
            }

            return true;
        }

        private bool CreateTerminateSequenceReplyHelper()
        {
            if (State == CommunicationState.Faulted || Aborted)
            {
                return false;
            }

            if (_terminateSequenceReplyHelper == null)
            {
                _terminateSequenceReplyHelper = new ReplyHelper(this,
                    TerminateSequenceReplyProvider.Instance, false);
            }

            return true;
        }

        private bool ContainsRequest(long requestSeqNum)
        {
            lock (ThisLock)
            {
                bool haveRequestInDictionary = _requestsByRequestSequenceNumber.ContainsKey(requestSeqNum);

                if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
                {
                    return haveRequestInDictionary
                        || ((_lastReply != null) && (_lastReply.RequestSequenceNumber == requestSeqNum) && (!_lastReplyAcked));
                }
                else
                {
                    return haveRequestInDictionary;
                }
            }
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IReplySessionChannel))
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

            T innerProperty = _binder.Channel.GetProperty<T>();
            if ((innerProperty == null) && (typeof(T) == typeof(FaultConverter)))
            {
                return (T)(object)FaultConverter.GetDefaultFaultConverter(_serviceDispatcher.MessageVersion);
            }
            else
            {
                return innerProperty;
            }
        }

        // This will be integrated into DispatchAsync logic
        //private bool HandleReceiveComplete(IAsyncResult result)
        //{
        //    RequestContext context;

        //    if (Binder.EndTryReceive(result, out context))
        //    {
        //        if (context == null)
        //        {
        //            bool terminated = false;

        //            lock (ThisLock)
        //            {
        //                terminated = _connection.Terminate();
        //            }

        //            if (!terminated && (Binder.State == CommunicationState.Opened))
        //            {
        //                Exception e = new CommunicationException(SR.EarlySecurityClose);
        //                _session.OnLocalFault(e, (Message)null, null);
        //            }

        //            return false;
        //        }

        //        WsrmMessageInfo info = WsrmMessageInfo.Get(_listener.MessageVersion,
        //            _listener.ReliableMessagingVersion, _binder.Channel, _binder.GetInnerSession(),
        //            context.RequestMessage);

        //        StartReceiving(false);
        //        ProcessRequestAsync(context, info);
        //        return false;
        //    }

        //    return true;
        //}

        // TODO: Aborting likely sends a message, should some aspect of this be async?
        protected override void OnAbort()
        {
            if (_closeSequenceReplyHelper != null)
            {
                _closeSequenceReplyHelper.Abort();
            }

            _connection.Abort(this);
            if (_terminateSequenceReplyHelper != null)
            {
                _terminateSequenceReplyHelper.Abort();
            }
            _session.Abort();
            AbortContexts();
            if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                _messagingCompleteWaitObject.Abort(this);
            }
            _serviceDispatcher.OnReliableChannelAbort(_session.InputID, _session.OutputID);
            base.OnAbort();
        }

        private void OnBinderException(IReliableChannelBinder sender, Exception exception)
        {
            if (exception is QuotaExceededException)
                _session.OnLocalFault(exception, (Message)null, null);
            else
                Enqueue(exception, null);
        }

        private void OnBinderFaulted(IReliableChannelBinder sender, Exception exception)
        {
            _binder.Abort();

            exception = new CommunicationException(SR.EarlySecurityFaulted, exception);
            _session.OnLocalFault(exception, (Message)null, null);
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            ThrowIfCloseInvalid();
            await CloseOutputAsync(token);
            if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                await _connection.CloseAsync(token);
                await _messagingCompleteWaitObject.WaitAsync(token);
            }
            else
            {
                await TerminateSequenceAsync(token);
                await _connection.CloseAsync(token);
            }

            await _session.CloseAsync(token);
            await _binder.CloseAsync(token, MaskingMode.Handled);
            await _serviceDispatcher.OnReliableChannelCloseAsync(_session.InputID, _session.OutputID, token);
            _serviceScope.Dispose();
            await base.OnCloseAsync(token);
        }

        protected override void OnClosed()
        {
            _deliveryStrategy.Dispose();
            _binder.Faulted -= OnBinderFaulted;

            if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                if (_lastReply != null)
                {
                    _lastReply.Abort();
                }
            }

            base.OnClosed();
        }

        protected override void OnFaulted()
        {
            _session.OnFaulted();
            UnblockClose();
            base.OnFaulted();
            //if (PerformanceCounters.PerformanceCountersEnabled)
            //    PerformanceCounters.SessionFaulted(_perfCounterId);
        }


        // This will be integrated into DispatchAsync logic
//        private static void OnReceiveCompletedStatic(IAsyncResult result)
//        {
//            if (result.CompletedSynchronously)
//                return;
//            ReliableReplySessionChannel channel = (ReliableReplySessionChannel)result.AsyncState;

//            try
//            {
//                if (channel.HandleReceiveComplete(result))
//                {
//                    channel.StartReceiving(true);
//                }
//            }
//#pragma warning suppress 56500 // covered by FxCOP
//            catch (Exception e)
//            {
//                if (Fx.IsFatal(e))
//                {
//                    throw;
//                }

//                channel._session.OnUnknownException(e);
//            }
//        }

        private void OnTerminateSequenceCompleted()
        {
            if ((_session.Settings.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
                && _connection.IsSequenceClosed)
            {
                lock (ThisLock)
                {
                    _connection.Terminate();
                }
            }
        }

        private bool PrepareReply(ReliableRequestContext context)
        {
            lock (ThisLock)
            {
                if (Aborted || State == CommunicationState.Faulted || State == CommunicationState.Closed)
                    return false;

                long requestSequenceNumber = context.RequestSequenceNumber;
                bool wsrmFeb2005 = _serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005;

                if (wsrmFeb2005 && (_connection.Last == requestSequenceNumber))
                {
                    if (_lastReply == null)
                        _lastReply = context;
                    _requestsByRequestSequenceNumber.Remove(requestSequenceNumber);
                    bool canReply = _connection.AllAdded && (State == CommunicationState.Closing);
                    if (!canReply)
                        return false;
                }
                else
                {
                    if (State == CommunicationState.Closing)
                        return false;

                    if (!context.HasReply)
                    {
                        _requestsByRequestSequenceNumber.Remove(requestSequenceNumber);
                        return true;
                    }
                }

                // won't throw if you do not need next sequence number
                if (_nextReplySequenceNumber == long.MaxValue)
                {
                    MessageNumberRolloverFault fault = new MessageNumberRolloverFault(_session.OutputID);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(fault.CreateException());
                }
                context.SetReplySequenceNumber(++_nextReplySequenceNumber);

                if (wsrmFeb2005 && (_connection.Last == requestSequenceNumber))
                {
                    if (!context.HasReply)
                        _lastReplyAcked = true;   //If Last ReplyAsync has no user data, it does not need to be acked. Here we just set it as its ack received.
                    _lastReplySequenceNumber = _nextReplySequenceNumber;
                    context.SetLastReply(_lastReplySequenceNumber);
                }
                else if (context.HasReply)
                {
                    _requestsByReplySequenceNumber.Add(_nextReplySequenceNumber, context);
                }

                return true;
            }
        }

        private Message PrepareReplyMessage(long replySequenceNumber, bool isLast, SequenceRangeCollection ranges, Message reply)
        {
            AddAcknowledgementHeader(reply);

            WsrmUtilities.AddSequenceHeader(
                _serviceDispatcher.ReliableMessagingVersion,
                reply,
                _session.OutputID,
                replySequenceNumber,
                isLast);

            return reply;
        }

        private void ProcessAcknowledgment(WsrmAcknowledgmentInfo info)
        {
            lock (ThisLock)
            {
                if (Aborted || State == CommunicationState.Faulted || State == CommunicationState.Closed)
                    return;

                if (_requestsByReplySequenceNumber.Count > 0)
                {
                    long reply;

                    _acked.Clear();

                    foreach (KeyValuePair<long, ReliableRequestContext> pair in _requestsByReplySequenceNumber)
                    {
                        reply = pair.Key;
                        if (info.Ranges.Contains(reply))
                        {
                            _acked.Add(reply);
                        }
                    }

                    for (int i = 0; i < _acked.Count; i++)
                    {
                        reply = _acked[i];
                        _requestsByRequestSequenceNumber.Remove(
                            _requestsByReplySequenceNumber[reply].RequestSequenceNumber);
                        _requestsByReplySequenceNumber.Remove(reply);
                    }

                    if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
                    {
                        if (!_lastReplyAcked && (_lastReplySequenceNumber != long.MinValue))
                        {
                            _lastReplyAcked = info.Ranges.Contains(_lastReplySequenceNumber);
                        }
                    }
                }
            }
        }

        private async Task ProcessAckRequestedAsync(RequestContext context)
        {
            try
            {
                using (Message reply = CreateAcknowledgement(_connection.Ranges))
                {
                    await context.ReplyAsync(reply);
                }
            }
            finally
            {
                context.RequestMessage.Close();
                await context.CloseAsync();
            }
        }

        private async Task ProcessShutdown11Async(RequestContext context, WsrmMessageInfo info)
        {
            bool cleanup = true;

            try
            {
                bool isTerminate = info.TerminateSequenceInfo != null;
                WsrmRequestInfo requestInfo = isTerminate
                    ? info.TerminateSequenceInfo
                    : info.CloseSequenceInfo;
                long last = isTerminate ? info.TerminateSequenceInfo.LastMsgNumber : info.CloseSequenceInfo.LastMsgNumber;

                if (!WsrmUtilities.ValidateWsrmRequest(_session, requestInfo, _binder, context))
                {
                    cleanup = false;
                    return;
                }

                bool scheduleShutdown = false;
                Exception remoteFaultException = null;
                ReplyHelper closeHelper = null;
                bool haveAllReplyAcks = true;
                bool isLastLargeEnough = true;
                bool isLastConsistent = true;

                lock (ThisLock)
                {
                    if (!_connection.IsLastKnown)
                    {
                        // All requests and replies must be acknowledged.
                        if (_requestsByRequestSequenceNumber.Count == 0)
                        {
                            if (isTerminate)
                            {
                                if (_connection.SetTerminateSequenceLast(last, out isLastLargeEnough))
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
                                scheduleShutdown = _connection.SetCloseSequenceLast(last);
                                isLastLargeEnough = scheduleShutdown;
                            }

                            if (scheduleShutdown)
                            {
                                // (1) !isTerminate && !IsLastKnown, CloseSequence received before TerminateSequenceAsync.
                                // - Need to ensure helper to delay the reply until Close.
                                // (2) isTerminate && !IsLastKnown, TerminateSequenceAsync received before CloseSequence.
                                // - Close not required, ensure it is created so we can bypass it.
                                if (!CreateCloseSequenceReplyHelper())
                                {
                                    return;
                                }

                                // Capture the helper in order to unblock it.
                                if (isTerminate)
                                {
                                    closeHelper = _closeSequenceReplyHelper;
                                }

                                _session.SetFinalAck(_connection.Ranges);
                                _deliveryStrategy.Dispose();
                            }
                        }
                        else
                        {
                            haveAllReplyAcks = false;
                        }
                    }
                    else
                    {
                        isLastConsistent = last == _connection.Last;
                    }
                }

                WsrmFault fault = null;

                if (!isLastLargeEnough)
                {
                    string faultString = SR.SequenceTerminatedSmallLastMsgNumber;
                    string exceptionString = SR.SmallLastMsgNumberExceptionString;
                    fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID, faultString,
                        exceptionString);
                }
                else if (!haveAllReplyAcks)
                {
                    string faultString = SR.SequenceTerminatedNotAllRepliesAcknowledged;
                    string exceptionString = SR.NotAllRepliesAcknowledgedExceptionString;
                    fault = SequenceTerminatedFault.CreateProtocolFault(_session.OutputID, faultString,
                        exceptionString);
                }
                else if (!isLastConsistent)
                {
                    string faultString = SR.SequenceTerminatedInconsistentLastMsgNumber;
                    string exceptionString = SR.InconsistentLastMsgNumberExceptionString;
                    fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID,
                        faultString, exceptionString);
                }
                else if (remoteFaultException != null)
                {
                    Message message = WsrmUtilities.CreateTerminateMessage(MessageVersion,
                        _serviceDispatcher.ReliableMessagingVersion, _session.OutputID);
                    AddAcknowledgementHeader(message);

                    using (message)
                    {
                        await context.ReplyAsync(message);
                    }

                    _session.OnRemoteFault(remoteFaultException);
                    return;
                }

                if (fault != null)
                {
                    _session.OnLocalFault(fault.CreateException(), fault, context);
                    cleanup = false;
                    return;
                }

                if (isTerminate)
                {
                    if (closeHelper != null)
                    {
                        await closeHelper.UnblockWaiterAsync();
                    }

                    lock (ThisLock)
                    {
                        if (!CreateTerminateSequenceReplyHelper())
                        {
                            return;
                        }
                    }
                }

                ReplyHelper replyHelper = isTerminate ? _terminateSequenceReplyHelper : _closeSequenceReplyHelper;

                if (!await replyHelper.TransferRequestContextAsync(context, info))
                {
                    await replyHelper.ReplyAsync(context, info, TimeoutHelper.GetCancellationToken(DefaultSendTimeout), MaskingMode.All);

                    if (isTerminate)
                    {
                        OnTerminateSequenceCompleted();
                    }
                }
                else
                {
                    cleanup = false;
                }

                if (scheduleShutdown)
                {
                    ActionItem.Schedule(ShutdownCallback, null);
                }
            }
            finally
            {
                if (cleanup)
                {
                    context.RequestMessage.Close();
                    await context.CloseAsync();
                }
            }
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

                _session.OnUnknownException(e);
            }
        }

        private async Task ProcessRequestAsync(RequestContext context, WsrmMessageInfo info)
        {
            bool closeMessage = true;
            bool closeContext = true;

            try
            {
                if (!await _session.ProcessInfoAsync(info, context))
                {
                    closeMessage = false;
                    closeContext = false;
                    return;
                }

                if (!_session.VerifyDuplexProtocolElements(info, context))
                {
                    closeMessage = false;
                    closeContext = false;
                    return;
                }

                _session.OnRemoteActivity(false);

                if (info.CreateSequenceInfo != null)
                {
                    EndpointAddress acksTo;

                    if (WsrmUtilities.ValidateCreateSequence(info, _serviceDispatcher, _binder.Channel, out acksTo))
                    {
                        Message response = WsrmUtilities.CreateCreateSequenceResponse(_serviceDispatcher.MessageVersion,
                            _serviceDispatcher.ReliableMessagingVersion, true, info.CreateSequenceInfo,
                            _serviceDispatcher.Ordered, _session.InputID, acksTo);

                        using (context)
                        {
                            using (response)
                            {
                                if (Binder.AddressResponse(info.Message, response))
                                    await context.ReplyAsync(response, TimeoutHelper.GetCancellationToken(DefaultSendTimeout));
                            }
                        }
                    }
                    else
                    {
                        _session.OnLocalFault(info.FaultException, info.FaultReply, context);
                    }

                    closeContext = false;
                    return;
                }

                closeContext = false;
                if (info.AcknowledgementInfo != null)
                {
                    ProcessAcknowledgment(info.AcknowledgementInfo);
                    closeContext = info.Action == WsrmIndex.GetSequenceAcknowledgementActionString(_serviceDispatcher.ReliableMessagingVersion);
                }

                if (!closeContext)
                {
                    closeMessage = false;
                    if (info.SequencedMessageInfo != null)
                    {
                        await ProcessSequencedMessageAsync(context, info.Action, info.SequencedMessageInfo);
                    }
                    else if (info.TerminateSequenceInfo != null)
                    {
                        if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
                        {
                            await ProcessTerminateSequenceFeb2005Async(context, info);
                        }
                        else if (info.TerminateSequenceInfo.Identifier == _session.InputID)
                        {
                            await ProcessShutdown11Async(context, info);
                        }
                        else    // Identifier == OutputID
                        {
                            WsrmFault fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID,
                                SR.SequenceTerminatedUnsupportedTerminateSequence,
                                SR.UnsupportedTerminateSequenceExceptionString);

                            _session.OnLocalFault(fault.CreateException(), fault, context);
                            closeMessage = false;
                            closeContext = false;
                            return;
                        }
                    }
                    else if (info.CloseSequenceInfo != null)
                    {
                        await ProcessShutdown11Async(context, info);
                    }
                    else if (info.AckRequestedInfo != null)
                    {
                        await ProcessAckRequestedAsync(context);
                    }
                }

                if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
                {
                    if (IsMessagingCompleted)
                    {
                        _messagingCompleteWaitObject.Set();
                    }
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

        // A given reliable request can be in one of three states:
        // 1. Known and Processing: A ReliableRequestContext exists in requestTable but the outcome for
        //      for the request is unknown. Any transport request referencing this reliable request
        //      (by means of the sequence number) must be held until the outcome becomes known.
        // 2. Known and Processed: A ReliableRequestContext exists in the requestTable and the outcome for
        //      for the request is known. The ReliableRequestContext holds that outcome. Any transport requests
        //      referening this reliable request must send the response dictated by the outcome.
        // 3. Unknown: No ReliableRequestContext exists in the requestTable for the referenced reliable request.
        //      In this case a new ReliableRequestContext is added to the requestTable to await some outcome.
        //
        // There are 4 possible outcomes for a reliable request:
        //  a. It is captured and the user replies. Transport replies are then copies of the user's reply.
        //  b. It is captured and the user closes the context. Transport replies are then acknowledgments
        //      that include the sequence number of the reliable request.
        //  c. It is captured and and the user aborts the context. Transport contexts are then aborted.
        //  d. It is not captured. In this case an acknowledgment that includes all sequence numbers
        //      previously captured is sent. Note two sub-cases here:
        //          1. It is not captured because it is dropped (e.g. it doesn't fit in the buffer). In this
        //              case the reliable request's sequence number is not in the acknowledgment.
        //          2. It is not captured because it is a duplicate. In this case the reliable request's
        //              sequence number is included in the acknowledgment. 
        //
        // By following these rules it is possible to support one-way and two-operations without having
        // knowledge of them (the user drives using the request context we give them) and at the same time
        // it is possible to forget about past replies once acknowledgments for them are received.
        private async Task ProcessSequencedMessageAsync(RequestContext context, string action, WsrmSequencedMessageInfo info)
        {
            ReliableRequestContext reliableContext = null;
            WsrmFault fault = null;
            bool scheduleShutdown = false;
            bool wsrmFeb2005 = _serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005;
            bool wsrm11 = _serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11;
            long requestSequenceNumber = info.SequenceNumber;
            bool isLast = wsrmFeb2005 && info.LastMessage;
            bool isLastOnly = wsrmFeb2005 && (action == WsrmFeb2005Strings.LastMessageAction);
            bool isDupe;
            Message message = null;

            lock (ThisLock)
            {
                if (Aborted || State == CommunicationState.Faulted || State == CommunicationState.Closed)
                {
                    context.RequestMessage.Close();
                    context.Abort();
                    return;
                }

                isDupe = _connection.Ranges.Contains(requestSequenceNumber);

                if (!_connection.IsValid(requestSequenceNumber, isLast))
                {
                    if (wsrmFeb2005)
                    {
                        fault = new LastMessageNumberExceededFault(_session.InputID);
                    }
                    else
                    {
                        message = CreateSequenceClosedFault();

                        //if (PerformanceCounters.PerformanceCountersEnabled)
                        //    PerformanceCounters.MessageDropped(_perfCounterId);
                    }
                }
                else if (isDupe)
                {
                    //if (PerformanceCounters.PerformanceCountersEnabled)
                    //    PerformanceCounters.MessageDropped(_perfCounterId);

                    if (!_requestsByRequestSequenceNumber.TryGetValue(info.SequenceNumber, out reliableContext))
                    {
                        if ((_lastReply != null) && (_lastReply.RequestSequenceNumber == info.SequenceNumber))
                            reliableContext = _lastReply;
                        else
                            reliableContext = new ReliableRequestContext(context, info.SequenceNumber, this, true);
                    }

                    reliableContext.SetAckRanges(_connection.Ranges);
                }
                else if ((State == CommunicationState.Closing) && !isLastOnly)
                {
                    if (wsrmFeb2005)
                    {
                        fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID,
                            SR.SequenceTerminatedSessionClosedBeforeDone,
                            SR.SessionClosedBeforeDone);
                    }
                    else
                    {
                        message = CreateSequenceClosedFault();
                        //if (PerformanceCounters.PerformanceCountersEnabled)
                        //    PerformanceCounters.MessageDropped(_perfCounterId);
                    }
                }
                // In the unordered case we accept no more than MaxSequenceRanges ranges to limit the
                // serialized ack size and the amount of memory taken by the ack ranges. In the
                // ordered case, the delivery strategy MaxTransferWindowSize quota mitigates this
                // threat.
                else if (_deliveryStrategy.CanEnqueue(requestSequenceNumber)
                    && (_requestsByReplySequenceNumber.Count < _serviceDispatcher.MaxTransferWindowSize)
                    && (_serviceDispatcher.Ordered || _connection.CanMerge(requestSequenceNumber)))
                {
                    _connection.Merge(requestSequenceNumber, isLast);
                    reliableContext = new ReliableRequestContext(context, info.SequenceNumber, this, false);
                    reliableContext.SetAckRanges(_connection.Ranges);

                    if (!isLastOnly)
                    {
                        _deliveryStrategy.Enqueue(reliableContext, requestSequenceNumber);
                        _requestsByRequestSequenceNumber.Add(info.SequenceNumber, reliableContext);
                    }
                    else
                    {
                        _lastReply = reliableContext;
                    }

                    scheduleShutdown = _connection.AllAdded;
                }
                else
                {
                    //if (PerformanceCounters.PerformanceCountersEnabled)
                    //    PerformanceCounters.MessageDropped(_perfCounterId);
                }
            }

            if (fault != null)
            {
                _session.OnLocalFault(fault.CreateException(), fault, context);
                return;
            }

            if (reliableContext == null)
            {
                if (message != null)
                {
                    using (message)
                    {
                        await context.ReplyAsync(message);
                    }
                }

                context.RequestMessage.Close();
                await context.CloseAsync();
                return;
            }

            if (isDupe && reliableContext.CheckForReplyOrAddInnerContext(context))
            {
                await reliableContext.SendReplyAsync(context, MaskingMode.All);
                return;
            }

            if (!isDupe && isLastOnly)
            {
                await reliableContext.CloseAsync();
            }

            if (scheduleShutdown)
            {
                ActionItem.Schedule(ShutdownCallback, null);
            }
        }

        private async Task ProcessTerminateSequenceFeb2005Async(RequestContext context, WsrmMessageInfo info)
        {
            bool cleanup = true;

            try
            {
                Message message = null;
                bool isTerminateEarly;
                bool haveAllReplyAcks;

                lock (ThisLock)
                {
                    isTerminateEarly = !_connection.Terminate();
                    haveAllReplyAcks = _requestsByRequestSequenceNumber.Count == 0;
                }

                WsrmFault fault = null;

                if (isTerminateEarly)
                {
                    fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID,
                        SR.SequenceTerminatedEarlyTerminateSequence,
                        SR.EarlyTerminateSequence);
                }
                else if (!haveAllReplyAcks)
                {
                    fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID,
                        SR.SequenceTerminatedBeforeReplySequenceAcked,
                        SR.EarlyRequestTerminateSequence);
                }

                if (fault != null)
                {
                    _session.OnLocalFault(fault.CreateException(), fault, context);
                    cleanup = false;
                    return;
                }

                message = WsrmUtilities.CreateTerminateMessage(MessageVersion,
                    _serviceDispatcher.ReliableMessagingVersion, _session.OutputID);
                AddAcknowledgementHeader(message);

                using (message)
                {
                    await context.ReplyAsync(message);
                }
            }
            finally
            {
                if (cleanup)
                {
                    context.RequestMessage.Close();
                    await context.CloseAsync();
                }
            }
        }

        private void ShutdownCallback(object state)
        {
            Shutdown();
        }

        private async Task TerminateSequenceAsync(CancellationToken token)
        {
            lock (ThisLock)
            {
                ThrowIfClosed();
                CreateTerminateSequenceReplyHelper();
            }

            await _terminateSequenceReplyHelper.WaitAndReplyAsync(token);
            OnTerminateSequenceCompleted();
        }

        private void ThrowIfCloseInvalid()
        {
            bool shouldFault = false;

            if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                if (PendingRequestContexts != 0 || _connection.Ranges.Count > 1)
                {
                    shouldFault = true;
                }
            }
            else if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                if (PendingRequestContexts != 0)
                {
                    shouldFault = true;
                }
            }

            if (shouldFault)
            {
                WsrmFault fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID,
                    SR.SequenceTerminatedSessionClosedBeforeDone, SR.SessionClosedBeforeDone);
                _session.OnLocalFault(null, fault, null);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(fault.CreateException());
            }
        }

        private void UnblockClose()
        {
            AbortContexts();

            if (_serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                _messagingCompleteWaitObject.Fault(this);
            }
            else
            {
                if (_closeSequenceReplyHelper != null)
                {
                    _closeSequenceReplyHelper.Fault();
                }
                if (_terminateSequenceReplyHelper != null)
                {
                    _terminateSequenceReplyHelper.Fault();
                }
            }

            _connection.Fault(this);
        }

        public override async Task DispatchAsync(RequestContext context)
        {
            if (context == null)
            {
                bool terminated = false;

                lock (ThisLock)
                {
                    terminated = _connection.Terminate();
                }

                if (!terminated && (Binder.State == CommunicationState.Opened))
                {
                    Exception e = new CommunicationException(SR.EarlySecurityClose);
                    _session.OnLocalFault(e, (Message)null, null);
                }
                return;
            }

            WsrmMessageInfo info = WsrmMessageInfo.Get(_serviceDispatcher.MessageVersion,
                _serviceDispatcher.ReliableMessagingVersion, _binder.Channel, _binder.GetInnerSession(),
                context.RequestMessage);

            await ProcessRequestAsync(context, info);
        }

        private class ReliableRequestContext : RequestContextBase
        {
            private MessageBuffer _bufferedReply;
            private readonly ReliableReplySessionChannel _channel;
            private readonly List<RequestContext> _innerContexts = new List<RequestContext>();
            private bool _isLastReply;
            private bool _outcomeKnown;
            private SequenceRangeCollection _ranges;
            private readonly long _requestSequenceNumber;
            private long _replySequenceNumber;

            public ReliableRequestContext(RequestContext context, long requestSequenceNumber, ReliableReplySessionChannel channel, bool outcome)
                : base(context.RequestMessage, channel.DefaultCloseTimeout, channel.DefaultSendTimeout)
            {
                _channel = channel;
                _requestSequenceNumber = requestSequenceNumber;
                _outcomeKnown = outcome;
                if (!outcome)
                    _innerContexts.Add(context);
            }

            public bool CheckForReplyOrAddInnerContext(RequestContext innerContext)
            {
                lock (ThisLock)
                {
                    if (_outcomeKnown)
                        return true;
                    _innerContexts.Add(innerContext);
                    return false;
                }
            }

            public bool HasReply => _bufferedReply != null;

            public long RequestSequenceNumber => _requestSequenceNumber;

            private void AbortInnerContexts()
            {
                for (int i = 0; i < _innerContexts.Count; i++)
                {
                    _innerContexts[i].Abort();
                    _innerContexts[i].RequestMessage.Close();
                }
                _innerContexts.Clear();
            }

            protected override void OnAbort()
            {
                bool outcome;
                lock (ThisLock)
                {
                    outcome = _outcomeKnown;
                    _outcomeKnown = true;
                }

                if (!outcome)
                {
                    AbortInnerContexts();
                }

                if (_channel.ContainsRequest(_requestSequenceNumber))
                {
                    Exception e = new ProtocolException(SR.ReliableRequestContextAborted);
                    _channel._session.OnLocalFault(e, (Message)null, null);
                }
            }

            protected override Task OnCloseAsync(CancellationToken cancellationToken)
            {
                // ReliableRequestContext.Close() relies on base.Close() to call reply if reply is not initiated. 
                if (!ReplyInitiated)
                {
                    return OnReplyAsync(null, cancellationToken);
                }

                return Task.CompletedTask;
            }

            protected override Task OnReplyAsync(Message reply, CancellationToken token)
            {
                return ReplyInternalAsync(reply, token);
            }

            internal async Task ReplyInternalAsync(Message reply, CancellationToken token)
            {
                bool needAbort = true;

                try
                {
                    lock (ThisLock)
                    {
                        if (_ranges == null)
                        {
                            throw Fx.AssertAndThrow("this.ranges != null");
                        }

                        if (Aborted)
                        {
                            needAbort = false;
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationObjectAbortedException(SRCommon.RequestContextAborted));
                        }

                        if (_outcomeKnown)
                        {
                            needAbort = false;
                            return;
                        }

                        if ((reply != null) && (_bufferedReply == null))
                            _bufferedReply = reply.CreateBufferedCopy(int.MaxValue);

                        if (!_channel.PrepareReply(this))
                        {
                            needAbort = false;
                            return;
                        }

                        _outcomeKnown = true;
                    }

                    for (int i = 0; i < _innerContexts.Count; i++)
                        await SendReplyAsync(_innerContexts[i], MaskingMode.Handled, token);
                    _innerContexts.Clear();
                    needAbort = false;
                }
                finally
                {
                    if (needAbort)
                    {
                        AbortInnerContexts();
                        Abort();
                    }
                }
            }

            public void SetAckRanges(SequenceRangeCollection ranges)
            {
                if (_ranges == null)
                    _ranges = ranges;
            }

            public void SetLastReply(long sequenceNumber)
            {
                _replySequenceNumber = sequenceNumber;
                _isLastReply = true;
                if (_bufferedReply == null)
                    _bufferedReply = Message.CreateMessage(_channel.MessageVersion, WsrmFeb2005Strings.LastMessageAction).CreateBufferedCopy(int.MaxValue);
            }

            public Task SendReplyAsync(RequestContext context, MaskingMode maskingMode)
            {
                return SendReplyAsync(context, maskingMode, TimeoutHelper.GetCancellationToken(DefaultSendTimeout));
            }

            private async Task SendReplyAsync(RequestContext context, MaskingMode maskingMode, CancellationToken token)
            {
                Message reply;

                if (!_outcomeKnown)
                {
                    throw Fx.AssertAndThrow("_outcomeKnown");
                }

                if (_bufferedReply != null)
                {
                    reply = _bufferedReply.CreateMessage();
                    _channel.PrepareReplyMessage(_replySequenceNumber, _isLastReply, _ranges, reply);
                }
                else
                {
                    reply = _channel.CreateAcknowledgement(_ranges);
                }

                _channel._binder.SetMaskingMode(context, maskingMode);

                using (reply)
                {
                    await context.ReplyAsync(reply, token);
                }

                await context.CloseAsync(token);
            }

            public void SetReplySequenceNumber(long sequenceNumber)
            {
                _replySequenceNumber = sequenceNumber;
            }
        }

        private class ReplyHelper
        {
            private Message _asyncMessage;
            private bool _canTransfer = true;
            private readonly ReliableReplySessionChannel _channel;
            private WsrmMessageInfo _info;
            private readonly ReplyProvider _replyProvider;
            private RequestContext _requestContext;
            private readonly bool _throwTimeoutOnWait;
            private readonly InterruptibleWaitObject _waitHandle;

            internal ReplyHelper(ReliableReplySessionChannel channel, ReplyProvider replyProvider, bool throwTimeoutOnWait)
            {
                _channel = channel;
                _replyProvider = replyProvider;
                _throwTimeoutOnWait = throwTimeoutOnWait;
                _waitHandle = new InterruptibleWaitObject(false, _throwTimeoutOnWait);
            }

            private object ThisLock => _channel.ThisLock;

            internal void Abort()
            {
                Cleanup(true);
            }

            private void Cleanup(bool abort)
            {
                lock (ThisLock)
                {
                    _canTransfer = false;
                }

                if (abort)
                {
                    _waitHandle.Abort(_channel);
                }
                else
                {
                    _waitHandle.Fault(_channel);
                }
            }

            internal void Fault()
            {
                Cleanup(false);
            }

            internal async Task ReplyAsync(RequestContext context, WsrmMessageInfo info, CancellationToken token, MaskingMode maskingMode)
            {
                using (Message message = _replyProvider.Provide(_channel, info))
                {
                    _channel._binder.SetMaskingMode(context, maskingMode);
                    await context.ReplyAsync(message, token);
                }
            }

            internal async Task<bool> TransferRequestContextAsync(RequestContext requestContext, WsrmMessageInfo info)
            {
                RequestContext oldContext = null;
                WsrmMessageInfo oldInfo = null;

                lock (ThisLock)
                {
                    if (!_canTransfer)
                    {
                        return false;
                    }
                    else
                    {
                        oldContext = _requestContext;
                        oldInfo = _info;
                        _requestContext = requestContext;
                        _info = info;
                    }
                }

                _waitHandle.Set();

                if (oldContext != null)
                {
                    oldInfo.Message.Close();
                    await oldContext.CloseAsync();
                }

                return true;
            }

            internal Task UnblockWaiterAsync()
            {
                return TransferRequestContextAsync(null, null);
            }

            internal async Task WaitAndReplyAsync(CancellationToken token)
            {
                await _waitHandle.WaitAsync(token);

                lock (ThisLock)
                {
                    _canTransfer = false;

                    if (_requestContext == null)
                    {
                        return;
                    }
                }

                await ReplyAsync(_requestContext, _info, token, MaskingMode.Handled);
                await _requestContext.CloseAsync(token);
            }
        }

        private abstract class ReplyProvider
        {
            internal abstract Message Provide(ReliableReplySessionChannel channel, WsrmMessageInfo info);
        }

        private class CloseSequenceReplyProvider : ReplyProvider
        {
            private static CloseSequenceReplyProvider s_instance = new CloseSequenceReplyProvider();

            private CloseSequenceReplyProvider()
            {
            }

            static internal ReplyProvider Instance
            {
                get
                {
                    if (s_instance == null)
                    {
                        s_instance = new CloseSequenceReplyProvider();
                    }

                    return s_instance;
                }
            }

            internal override Message Provide(ReliableReplySessionChannel channel, WsrmMessageInfo requestInfo)
            {
                Message message = WsrmUtilities.CreateCloseSequenceResponse(channel.MessageVersion,
                   requestInfo.CloseSequenceInfo.MessageId, channel._session.InputID);
                channel.AddAcknowledgementHeader(message);
                return message;
            }
        }

        private class TerminateSequenceReplyProvider : ReplyProvider
        {
            private static TerminateSequenceReplyProvider s_instance = new TerminateSequenceReplyProvider();

            private TerminateSequenceReplyProvider()
            {
            }

            static internal ReplyProvider Instance
            {
                get
                {
                    if (s_instance == null)
                    {
                        s_instance = new TerminateSequenceReplyProvider();
                    }

                    return s_instance;
                }
            }

            internal override Message Provide(ReliableReplySessionChannel channel, WsrmMessageInfo requestInfo)
            {
                Message message = WsrmUtilities.CreateTerminateResponseMessage(channel.MessageVersion,
                   requestInfo.TerminateSequenceInfo.MessageId, channel._session.InputID);
                channel.AddAcknowledgementHeader(message);
                return message;
            }
        }
    }
}
