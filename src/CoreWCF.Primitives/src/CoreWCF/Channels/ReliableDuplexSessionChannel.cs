// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class ReliableDuplexSessionChannel : InputQueueDuplexChannel, IDuplexSessionChannel
    {
        private readonly ReliableServiceDispatcherBase<IDuplexSessionChannel> _serviceDispatcher;
        private bool _acknowledgementScheduled = false;
        private readonly IOThreadTimer _acknowledgementTimer;
        private ulong _ackVersion = 1;
        private bool _advertisedZero = false;
        private readonly IReliableChannelBinder _binder;
        private InterruptibleWaitObject _closeOutputWaitObject;
        private SendWaitReliableRequestor _closeRequestor;
        private DeliveryStrategy<Message> _deliveryStrategy;
        private readonly Guard _guard = new Guard(int.MaxValue);
        private ReliableInputConnection _inputConnection;
        private Exception _maxRetryCountException = null;
        private ReliableOutputConnection _outputConnection;
        private int _pendingAcknowledgements = 0;
        private ChannelReliableSession _session;
        private readonly IReliableFactorySettings _settings;
        private SendWaitReliableRequestor _terminateRequestor;

        protected ReliableDuplexSessionChannel(
            ReliableServiceDispatcherBase<IDuplexSessionChannel> serviceDispatcher,
            IReliableChannelBinder binder)
            : base(serviceDispatcher, serviceDispatcher, binder.LocalAddress)
        {
            _binder = binder;
            _serviceDispatcher = serviceDispatcher;
            _acknowledgementTimer = new IOThreadTimer(new Action<object>(OnAcknowledgementTimeoutElapsed), null, true);
            _binder.Faulted += OnBinderFaulted;
            _binder.OnException += OnBinderException;
        }

        public IReliableChannelBinder Binder => _binder;
        public override EndpointAddress LocalAddress => _binder.LocalAddress;
        protected ReliableOutputConnection OutputConnection => _outputConnection;
        protected UniqueId OutputID => _session.OutputID;
        protected ChannelReliableSession ReliableSession => _session;
        public override EndpointAddress RemoteAddress => _binder.RemoteAddress;
        protected IReliableFactorySettings Settings => _settings;
        public override Uri Via => RemoteAddress.Uri;
        public IDuplexSession Session => (IDuplexSession)_session;

        private void AddPendingAcknowledgements(Message message)
        {
            lock (ThisLock)
            {
                if (_pendingAcknowledgements > 0)
                {
                    _acknowledgementTimer.Cancel();
                    _acknowledgementScheduled = false;
                    _pendingAcknowledgements = 0;
                    _ackVersion++;

                    int bufferRemaining = GetBufferRemaining();

                    WsrmUtilities.AddAcknowledgementHeader(
                        _settings.ReliableMessagingVersion,
                        message,
                        _session.InputID,
                        _inputConnection.Ranges,
                        _inputConnection.IsLastKnown,
                        bufferRemaining);
                }
            }
        }

        private Task CloseSequenceAsync(CancellationToken token)
        {
            CreateCloseRequestor();
            return _closeRequestor.RequestAsync(token);
            // reply came from receive loop, receive loop owns verified message so nothing more to do.
        }

        private void ConfigureRequestor(ReliableRequestor requestor)
        {
            requestor.MessageVersion = _settings.MessageVersion;
            requestor.Binder = _binder;
            requestor.SetRequestResponsePattern();
        }

        private Message CreateAcknowledgmentMessage()
        {
            lock (ThisLock)
                _ackVersion++;

            int bufferRemaining = GetBufferRemaining();

            Message message = WsrmUtilities.CreateAcknowledgmentMessage(Settings.MessageVersion,
                Settings.ReliableMessagingVersion, _session.InputID, _inputConnection.Ranges,
                _inputConnection.IsLastKnown, bufferRemaining);

            //if (TD.SequenceAcknowledgementSentIsEnabled())
            //{
            //    TD.SequenceAcknowledgementSent(_session.Id);
            //}

            return message;
        }

        private void CreateCloseRequestor()
        {
            SendWaitReliableRequestor temp = new SendWaitReliableRequestor();

            ConfigureRequestor(temp);
            temp.TimeoutString1Index = SR.TimeoutOnClose;
            temp.MessageAction = WsrmIndex.GetCloseSequenceActionHeader(
                _settings.MessageVersion.Addressing);
            temp.MessageBody = new CloseSequence(_session.OutputID, _outputConnection.Last);

            lock (ThisLock)
            {
                ThrowIfClosed();
                _closeRequestor = temp;
            }
        }

        private void CreateTerminateRequestor()
        {
            SendWaitReliableRequestor temp = new SendWaitReliableRequestor();

            ConfigureRequestor(temp);
            ReliableMessagingVersion reliableMessagingVersion = _settings.ReliableMessagingVersion;
            temp.MessageAction = WsrmIndex.GetTerminateSequenceActionHeader(
                _settings.MessageVersion.Addressing, reliableMessagingVersion);
            temp.MessageBody = new TerminateSequence(reliableMessagingVersion, _session.OutputID,
                _outputConnection.Last);

            lock (ThisLock)
            {
                ThrowIfClosed();
                _terminateRequestor = temp;

                if (_inputConnection.IsLastKnown)
                {
                    _session.CloseSession();
                }
            }
        }

        private int GetBufferRemaining()
        {
            int bufferRemaining = -1;

            if (_settings.FlowControlEnabled)
            {
                bufferRemaining = _settings.MaxTransferWindowSize - _deliveryStrategy.EnqueuedCount;
                _advertisedZero = (bufferRemaining == 0);
            }

            return bufferRemaining;
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IDuplexSessionChannel))
            {
                return (T)(object)this;
            }

            T baseProperty = base.GetProperty<T>();
            if (baseProperty != null)
            {
                return baseProperty;
            }

            T innerProperty = _binder.Channel.GetProperty<T>();
            if ((innerProperty == null) && (typeof(T) == typeof(FaultConverter)))
            {
                return (T)(object)FaultConverter.GetDefaultFaultConverter(_settings.MessageVersion);
            }
            else
            {
                return innerProperty;
            }
        }

        private async Task InternalCloseOutputSessionAsync(CancellationToken token)
        {
            await _outputConnection.CloseAsync(token);

            if (_settings.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                await CloseSequenceAsync(token);
            }

            await TerminateSequenceAsync(token);
        }

        protected virtual void OnRemoteActivity()
        {
            _session.OnRemoteActivity(false);
        }

        private WsrmFault ProcessCloseOrTerminateSequenceResponse(bool close, WsrmMessageInfo info)
        {
            SendWaitReliableRequestor requestor = close ? _closeRequestor : _terminateRequestor;

            if (requestor != null)
            {
                WsrmFault fault = close
                    ? WsrmUtilities.ValidateCloseSequenceResponse(_session, _closeRequestor.MessageId, info,
                    _outputConnection.Last)
                    : WsrmUtilities.ValidateTerminateSequenceResponse(_session, _terminateRequestor.MessageId,
                    info, _outputConnection.Last);

                if (fault != null)
                {
                    return fault;
                }

                requestor.SetInfo(info);
                return null;
            }

            string request = close ? Wsrm11Strings.CloseSequence : WsrmFeb2005Strings.TerminateSequence;
            string faultString = SR.Format(SR.ReceivedResponseBeforeRequestFaultString, request);
            string exceptionString = SR.Format(SR.ReceivedResponseBeforeRequestExceptionString, request);
            return SequenceTerminatedFault.CreateProtocolFault(_session.OutputID, faultString, exceptionString);
        }

        protected async Task ProcessDuplexMessageAsync(WsrmMessageInfo info)
        {
            bool closeMessage = true;

            try
            {
                bool wsrmFeb2005 = _settings.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005;
                bool wsrm11 = _settings.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11;
                bool final = false;

                if (_outputConnection != null && info.AcknowledgementInfo != null)
                {
                    final = wsrm11 && info.AcknowledgementInfo.Final;

                    int bufferRemaining = -1;

                    if (_settings.FlowControlEnabled)
                        bufferRemaining = info.AcknowledgementInfo.BufferRemaining;

                    _outputConnection.ProcessTransferred(info.AcknowledgementInfo.Ranges, bufferRemaining);
                }

                OnRemoteActivity();

                bool tryAckNow = (info.AckRequestedInfo != null);
                bool forceAck = false;
                bool terminate = false;
                bool scheduleShutdown = false;
                ulong oldAckVersion = 0;
                WsrmFault fault = null;
                Message message = null;
                Exception remoteFaultException = null;

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

                        if (!_inputConnection.IsValid(sequenceNumber, isLast))
                        {
                            if (wsrmFeb2005)
                            {
                                fault = new LastMessageNumberExceededFault(ReliableSession.InputID);
                            }
                            else
                            {
                                message = new SequenceClosedFault(_session.InputID).CreateMessage(
                                    _settings.MessageVersion, _settings.ReliableMessagingVersion);
                                forceAck = true;

                                OnMessageDropped();
                            }
                        }
                        else if (_inputConnection.Ranges.Contains(sequenceNumber))
                        {
                            OnMessageDropped();
                            tryAckNow = true;
                        }
                        else if (wsrmFeb2005 && info.Action == WsrmFeb2005Strings.LastMessageAction)
                        {
                            _inputConnection.Merge(sequenceNumber, isLast);

                            if (_inputConnection.AllAdded)
                            {
                                scheduleShutdown = true;

                                if (_outputConnection.CheckForTermination())
                                {
                                    _session.CloseSession();
                                }
                            }
                        }
                        else if (State == CommunicationState.Closing)
                        {
                            if (wsrmFeb2005)
                            {
                                fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID,
                                    SR.SequenceTerminatedSessionClosedBeforeDone,
                                    SR.SessionClosedBeforeDone);
                            }
                            else
                            {
                                message = new SequenceClosedFault(_session.InputID).CreateMessage(
                                    _settings.MessageVersion, _settings.ReliableMessagingVersion);
                                forceAck = true;

                                OnMessageDropped();
                            }
                        }
                        // In the unordered case we accept no more than MaxSequenceRanges ranges to limit the
                        // serialized ack size and the amount of memory taken by the ack ranges. In the
                        // ordered case, the delivery strategy MaxTransferWindowSize quota mitigates this
                        // threat.
                        else if (_deliveryStrategy.CanEnqueue(sequenceNumber)
                            && (Settings.Ordered || _inputConnection.CanMerge(sequenceNumber)))
                        {
                            _inputConnection.Merge(sequenceNumber, isLast);
                            _deliveryStrategy.Enqueue(info.Message, sequenceNumber);
                            closeMessage = false;
                            oldAckVersion = _ackVersion;
                            _pendingAcknowledgements++;

                            if (_inputConnection.AllAdded)
                            {
                                scheduleShutdown = true;

                                if (_outputConnection.CheckForTermination())
                                {
                                    _session.CloseSession();
                                }
                            }
                        }
                        else
                        {
                            OnMessageDropped();
                        }

                        // if (ack now && we enqueued && an ack has been sent since we enqueued (and thus 
                        // carries the sequence number of the message we just processed)) then we don't
                        // need to ack again.
                        if (_inputConnection.IsLastKnown || _pendingAcknowledgements == _settings.MaxTransferWindowSize)
                            tryAckNow = true;

                        bool startTimer = tryAckNow || (_pendingAcknowledgements > 0 && fault == null);
                        if (startTimer && !_acknowledgementScheduled)
                        {
                            _acknowledgementScheduled = true;
                            _acknowledgementTimer.Set(_settings.AcknowledgementInterval);
                        }
                    }
                }
                else if (wsrmFeb2005 && info.TerminateSequenceInfo != null)
                {
                    bool isTerminateEarly;

                    lock (ThisLock)
                    {
                        isTerminateEarly = !_inputConnection.Terminate();
                    }

                    if (isTerminateEarly)
                    {
                        fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID,
                            SR.SequenceTerminatedEarlyTerminateSequence,
                            SR.EarlyTerminateSequence);
                    }
                }
                else if (wsrm11)
                {
                    if (((info.TerminateSequenceInfo != null) && (info.TerminateSequenceInfo.Identifier == _session.InputID))
                        || (info.CloseSequenceInfo != null))
                    {
                        bool isTerminate = info.TerminateSequenceInfo != null;
                        WsrmRequestInfo requestInfo = isTerminate
                            ? info.TerminateSequenceInfo
                            : info.CloseSequenceInfo;
                        long last = isTerminate ? info.TerminateSequenceInfo.LastMsgNumber : info.CloseSequenceInfo.LastMsgNumber;

                        if (!WsrmUtilities.ValidateWsrmRequest(_session, requestInfo, _binder, null))
                        {
                            return;
                        }

                        bool isLastLargeEnough = true;
                        bool isLastConsistent = true;

                        lock (ThisLock)
                        {
                            if (!_inputConnection.IsLastKnown)
                            {
                                if (isTerminate)
                                {
                                    if (_inputConnection.SetTerminateSequenceLast(last, out isLastLargeEnough))
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
                                    scheduleShutdown = _inputConnection.SetCloseSequenceLast(last);
                                    isLastLargeEnough = scheduleShutdown;
                                }

                                if (scheduleShutdown)
                                {
                                    _session.SetFinalAck(_inputConnection.Ranges);
                                    if (_terminateRequestor != null)
                                    {
                                        _session.CloseSession();
                                    }

                                    _deliveryStrategy.Dispose();
                                }
                            }
                            else
                            {
                                isLastConsistent = (last == _inputConnection.Last);

                                // Have seen CloseSequence already, TerminateSequence means cleanup.
                                if (isTerminate && isLastConsistent && _inputConnection.IsSequenceClosed)
                                {
                                    terminate = true;
                                }
                            }
                        }

                        if (!isLastLargeEnough)
                        {
                            string faultString = SR.SequenceTerminatedSmallLastMsgNumber;
                            string exceptionString = SR.SmallLastMsgNumberExceptionString;
                            fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID, faultString, exceptionString);
                        }
                        else if (!isLastConsistent)
                        {
                            string faultString = SR.SequenceTerminatedInconsistentLastMsgNumber;
                            string exceptionString = SR.InconsistentLastMsgNumberExceptionString;
                            fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID, faultString, exceptionString);
                        }
                        else
                        {
                            message = isTerminate
                                ? WsrmUtilities.CreateTerminateResponseMessage(_settings.MessageVersion,
                                requestInfo.MessageId, _session.InputID)
                                : WsrmUtilities.CreateCloseSequenceResponse(_settings.MessageVersion,
                                requestInfo.MessageId, _session.InputID);
                            forceAck = true;
                        }
                    }
                    else if (info.TerminateSequenceInfo != null)    // Identifier == OutputID
                    {
                        fault = SequenceTerminatedFault.CreateProtocolFault(_session.InputID,
                            SR.SequenceTerminatedUnsupportedTerminateSequence,
                            SR.UnsupportedTerminateSequenceExceptionString);
                    }
                    else if (info.TerminateSequenceResponseInfo != null)
                    {
                        fault = ProcessCloseOrTerminateSequenceResponse(false, info);
                    }
                    else if (info.CloseSequenceResponseInfo != null)
                    {
                        fault = ProcessCloseOrTerminateSequenceResponse(true, info);
                    }
                    else if (final)
                    {
                        if (_closeRequestor == null)
                        {
                            string exceptionString = SR.UnsupportedCloseExceptionString;
                            string faultString = SR.SequenceTerminatedUnsupportedClose;

                            fault = SequenceTerminatedFault.CreateProtocolFault(_session.OutputID, faultString,
                                exceptionString);
                        }
                        else
                        {
                            fault = WsrmUtilities.ValidateFinalAck(_session, info, _outputConnection.Last);

                            if (fault == null)
                            {
                                _closeRequestor.SetInfo(info);
                            }
                        }
                    }
                    else if (info.WsrmHeaderFault != null)
                    {
                        if (!(info.WsrmHeaderFault is UnknownSequenceFault))
                        {
                            throw Fx.AssertAndThrow("Fault must be UnknownSequence fault.");
                        }

                        if (_terminateRequestor == null)
                        {
                            throw Fx.AssertAndThrow("In wsrm11, if we start getting UnknownSequence, terminateRequestor cannot be null.");
                        }

                        _terminateRequestor.SetInfo(info);
                    }
                }

                if (fault != null)
                {
                    _session.OnLocalFault(fault.CreateException(), fault, null);
                    return;
                }

                if (message != null)
                {
                    if (forceAck)
                    {
                        WsrmUtilities.AddAcknowledgementHeader(_settings.ReliableMessagingVersion, message,
                            _session.InputID, _inputConnection.Ranges, true, GetBufferRemaining());
                    }
                    else if (tryAckNow)
                    {
                        AddPendingAcknowledgements(message);
                    }
                }
                else if (tryAckNow)
                {
                    lock (ThisLock)
                    {
                        if (oldAckVersion != 0 && oldAckVersion != _ackVersion)
                            return;

                        if (_acknowledgementScheduled)
                        {
                            _acknowledgementTimer.Cancel();
                            _acknowledgementScheduled = false;
                        }
                        _pendingAcknowledgements = 0;
                    }

                    message = CreateAcknowledgmentMessage();
                }

                if (message != null)
                {
                    using (message)
                    {
                        if (await _guard.EnterAsync())
                        {
                            try
                            {
                                await _binder.SendAsync(message, TimeoutHelper.GetCancellationToken(DefaultSendTimeout));
                            }
                            finally
                            {
                                _guard.Exit();
                            }
                        }
                    }
                }

                if (terminate)
                {
                    lock (ThisLock)
                    {
                        _inputConnection.Terminate();
                    }
                }

                if (remoteFaultException != null)
                {
                    ReliableSession.OnRemoteFault(remoteFaultException);
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

        protected abstract Task ProcessMessageAsync(WsrmMessageInfo info);

        protected override void OnAbort()
        {
            if (_outputConnection != null)
                _outputConnection.Abort(this);

            if (_inputConnection != null)
                _inputConnection.Abort(this);

            _guard.Abort();

            ReliableRequestor tempRequestor = _closeRequestor;
            if (tempRequestor != null)
            {
                tempRequestor.Abort(this);
            }

            tempRequestor = _terminateRequestor;
            if (tempRequestor != null)
            {
                tempRequestor.Abort(this);
            }

            _session.Abort();
        }

        private void OnAcknowledgementTimeoutElapsed(object state)
        {
            lock (ThisLock)
            {
                _acknowledgementScheduled = false;
                _pendingAcknowledgements = 0;

                if (State == CommunicationState.Closing
                    || State == CommunicationState.Closed
                    || State == CommunicationState.Faulted)
                    return;
            }

            _ = OnAcknowledgementTimeoutElapsedAsync();
        }

        private async Task OnAcknowledgementTimeoutElapsedAsync()
        {
            if (await _guard.EnterAsync())
            {
                try
                {
                    using (Message message = CreateAcknowledgmentMessage())
                    {
                        await _binder.SendAsync(message, TimeoutHelper.GetCancellationToken(DefaultSendTimeout));
                    }
                }
                finally
                {
                    _guard.Exit();
                }
            }
        }

        private void OnBinderException(IReliableChannelBinder sender, Exception exception)
        {
            if (exception is QuotaExceededException)
            {
                if (State == CommunicationState.Opening ||
                    State == CommunicationState.Opened ||
                    State == CommunicationState.Closing)
                {
                    _session.OnLocalFault(exception, SequenceTerminatedFault.CreateQuotaExceededFault(_session.OutputID), null);
                }
            }
            else
            {
                Enqueue(exception, null);
            }
        }

        private void OnBinderFaulted(IReliableChannelBinder sender, Exception exception)
        {
            _binder.Abort();

            if (State == CommunicationState.Opening ||
                State == CommunicationState.Opened ||
                State == CommunicationState.Closing)
            {
                exception = new CommunicationException(SR.EarlySecurityFaulted, exception);
                _session.OnLocalFault(exception, (Message)null, null);
            }
        }

        // CloseOutputSession && Close: CloseOutputSession only closes the ReliableOutputConnection
        // from the Opened state, if it does, it must create the closeOutputWaitObject so that
        // close may properly synchronize. If no closeOutputWaitObject is present, Close may close
        // the ROC safely since it is in the Closing state.
        protected override async Task OnCloseAsync(CancellationToken token)
        {
            ThrowIfCloseInvalid();

            if (_outputConnection != null)
            {
                if (_closeOutputWaitObject != null)
                {
                    await _closeOutputWaitObject.WaitAsync(token);
                }
                else
                {
                    await InternalCloseOutputSessionAsync(token);
                }

                await _inputConnection.CloseAsync(token);
            }

            await _guard.CloseAsync(token);
            await _session.CloseAsync(token);
            await _binder.CloseAsync(token, MaskingMode.Handled);
            await base.OnCloseAsync(token);
        }

        protected async Task OnCloseOutputSessionAsync(CancellationToken token)
        {
            lock (ThisLock)
            {
                ThrowIfNotOpened();
                ThrowIfFaulted();

                if ((State != CommunicationState.Opened)
                    || (_closeOutputWaitObject != null))
                {
                    return;
                }

                _closeOutputWaitObject = new InterruptibleWaitObject(false, true);
            }

            bool throwing = true;

            try
            {
                await InternalCloseOutputSessionAsync(token);
                throwing = false;
            }
            finally
            {
                if (throwing)
                {
                    _session.OnLocalFault(null, SequenceTerminatedFault.CreateCommunicationFault(_session.OutputID, SR.CloseOutputSessionErrorReason, null), null);
                    _closeOutputWaitObject.Fault(this);
                }
                else
                {
                    _closeOutputWaitObject.Set();
                }
            }
        }

        protected override void OnClosed()
        {
            base.OnClosed();

            _binder.Faulted -= OnBinderFaulted;
            if (_deliveryStrategy != null)
                _deliveryStrategy.Dispose();
        }

        protected override void OnClosing()
        {
            base.OnClosing();
            _acknowledgementTimer.Cancel();
        }

        private void OnComponentFaulted(Exception faultException, WsrmFault fault)
        {
            _session.OnLocalFault(faultException, fault, null);
        }

        private void OnComponentException(Exception exception)
        {
            ReliableSession.OnUnknownException(exception);
        }

        protected override void OnFaulted()
        {
            _session.OnFaulted();
            UnblockClose();
            base.OnFaulted();
        }

        protected override async Task OnSendAsync(Message message, CancellationToken token)
        {
            if (!await _outputConnection.AddMessageAsync(message, token, null))
                ThrowInvalidAddException();
        }

        private async Task OnSendHandlerAsync(MessageAttemptInfo attemptInfo, CancellationToken token, bool maskUnhandledException)
        {
            using (attemptInfo.Message)
            {
                if (attemptInfo.RetryCount > _settings.MaxRetryCount)
                {
                    _session.OnLocalFault(new CommunicationException(SR.MaximumRetryCountExceeded, _maxRetryCountException),
                        SequenceTerminatedFault.CreateMaxRetryCountExceededFault(_session.OutputID), null);
                }
                else
                {
                    _session.OnLocalActivity();
                    this.AddPendingAcknowledgements(attemptInfo.Message);

                    MaskingMode maskingMode = maskUnhandledException ? MaskingMode.Unhandled : MaskingMode.None;

                    if (attemptInfo.RetryCount < _settings.MaxRetryCount)
                    {
                        maskingMode |= MaskingMode.Handled;
                        await _binder.SendAsync(attemptInfo.Message, token, maskingMode);
                    }
                    else
                    {
                        try
                        {
                            await _binder.SendAsync(attemptInfo.Message, token, maskingMode);
                        }
                        catch (Exception e)
                        {
                            if (Fx.IsFatal(e))
                                throw;

                            if (_binder.IsHandleable(e))
                            {
                                _maxRetryCountException = e;
                            }
                            else
                            {
                                throw;
                            }
                        }
                    }
                }
            }
        }

        private async Task OnSendAckRequestedHandlerAsync(CancellationToken token)
        {
            _session.OnLocalActivity();
            using (Message message = WsrmUtilities.CreateAckRequestedMessage(Settings.MessageVersion,
                Settings.ReliableMessagingVersion, ReliableSession.OutputID))
            {
                await _binder.SendAsync(message, token, MaskingMode.Handled);
            }
        }

        // Based on HandleReceiveComplete
        public override async Task DispatchAsync(RequestContext context)
        {
            if (context == null)
            {
                bool terminated = false;

                lock (ThisLock)
                {
                    terminated = _inputConnection.Terminate();
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

            WsrmMessageInfo info = WsrmMessageInfo.Get(_settings.MessageVersion,
                _settings.ReliableMessagingVersion, _binder.Channel, _binder.GetInnerSession(),
                message);

            await ProcessMessageAsync(info);
        }

        public override Task DispatchAsync(Message message)
        {
            WsrmMessageInfo info = WsrmMessageInfo.Get(_settings.MessageVersion,
                _settings.ReliableMessagingVersion, _binder.Channel, _binder.GetInnerSession(),
                message);

            return ProcessMessageAsync(info);
        }

        protected override void OnOpened()
        {
            base.OnOpened();
        }

        protected virtual void OnMessageDropped()
        {
        }

        protected void SetConnections()
        {
            _outputConnection = new ReliableOutputConnection(_session.OutputID,
                _settings.MaxTransferWindowSize, Settings.MessageVersion,
                Settings.ReliableMessagingVersion, _session.InitiationTime, true, DefaultSendTimeout);
            _outputConnection.Faulted += OnComponentFaulted;
            _outputConnection.OnException += OnComponentException;
            _outputConnection.AsyncSendHandler = OnSendHandlerAsync;
            _outputConnection.AsyncSendAckRequestedHandler = OnSendAckRequestedHandlerAsync;

            _inputConnection = new ReliableInputConnection();
            _inputConnection.ReliableMessagingVersion = Settings.ReliableMessagingVersion;

            if (_settings.Ordered)
                _deliveryStrategy = new OrderedDeliveryStrategy<Message>(this, _settings.MaxTransferWindowSize, false);
            else
                _deliveryStrategy = new UnorderedDeliveryStrategy<Message>(this, _settings.MaxTransferWindowSize);

            _deliveryStrategy.DequeueCallback = OnDeliveryStrategyItemDequeued;
        }

        protected void SetSession(ChannelReliableSession session)
        {
            session.UnblockChannelCloseCallback = UnblockClose;
            _session = session;
        }

        private void OnDeliveryStrategyItemDequeued()
        {
            if (_advertisedZero)
                OnAcknowledgementTimeoutElapsed(null);
        }

        private async Task TerminateSequenceAsync(CancellationToken token)
        {
            ReliableMessagingVersion reliableMessagingVersion = _settings.ReliableMessagingVersion;

            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                if (_outputConnection.CheckForTermination())
                {
                    _session.CloseSession();
                }

                Message message = WsrmUtilities.CreateTerminateMessage(_settings.MessageVersion,
                    reliableMessagingVersion, _session.OutputID);
                await _binder.SendAsync(message, token, MaskingMode.Handled);
            }
            else if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                CreateTerminateRequestor();
                await _terminateRequestor.RequestAsync(token);
                // reply came from receive loop, receive loop owns verified message so nothing more to do.
            }
            else
            {
                throw Fx.AssertAndThrow("Reliable messaging version not supported.");
            }
        }

        private void ThrowIfCloseInvalid()
        {
            bool shouldFault = false;

            if (_settings.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                if (_deliveryStrategy.EnqueuedCount > 0 || _inputConnection.Ranges.Count > 1)
                {
                    shouldFault = true;
                }
            }
            else if (_settings.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                if (_deliveryStrategy.EnqueuedCount > 0)
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

        private void ThrowInvalidAddException()
        {
            if (State == CommunicationState.Opened)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SendCannotBeCalledAfterCloseOutputSession));
            else if (State == CommunicationState.Faulted)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(GetTerminalException());
            else
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateClosedException());
        }

        private void UnblockClose()
        {
            if (_outputConnection != null)
            {
                _outputConnection.Fault(this);
            }

            if (_inputConnection != null)
            {
                _inputConnection.Fault(this);
            }

            ReliableRequestor tempRequestor = _closeRequestor;
            if (tempRequestor != null)
            {
                tempRequestor.Fault(this);
            }

            tempRequestor = _terminateRequestor;
            if (tempRequestor != null)
            {
                tempRequestor.Fault(this);
            }
        }
    }

    internal sealed class ServerReliableDuplexSessionChannel : ReliableDuplexSessionChannel
    {
        private ReliableServiceDispatcherBase<IDuplexSessionChannel> serviceDispatcher;

        public ServerReliableDuplexSessionChannel(
            ReliableServiceDispatcherBase<IDuplexSessionChannel> serviceDispatcher,
            IReliableChannelBinder binder, FaultHelper faultHelper,
            UniqueId inputID,
            UniqueId outputID)
            : base(serviceDispatcher, binder)
        {
            this.serviceDispatcher = serviceDispatcher;
            DuplexServerReliableSession session = new DuplexServerReliableSession(this, serviceDispatcher, faultHelper, inputID, outputID);
            SetSession(session);
            var openTask = session.OpenAsync(CancellationToken.None);
            // Make sure we aren't waiting on any async work
            Fx.Assert(openTask.IsCompleted, "DuplexServerReliableSession.OpenAsync is presumed to complete synchronously");
            openTask.GetAwaiter().GetResult();
            SetConnections();

            //if (PerformanceCounters.PerformanceCountersEnabled)
            //    perfCounterId = this.listener.Uri.ToString().ToUpperInvariant();
       }

        // Close/Abort: The base Close/Abort is called first because it is shutting down the
        // channel. Shutting down the server state should be done after shutting down the channel.
        protected override void OnAbort()
        {
            base.OnAbort();
            serviceDispatcher.OnReliableChannelAbort(ReliableSession.InputID,
                ReliableSession.OutputID);
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            await base.OnCloseAsync(token);
            await serviceDispatcher.OnReliableChannelCloseAsync(ReliableSession.InputID,
                ReliableSession.OutputID, token);
        }


        protected override Task OnOpenAsync(CancellationToken token) => Task.CompletedTask;

        protected override void OnFaulted()
        {
            base.OnFaulted();
            //if (PerformanceCounters.PerformanceCountersEnabled)
            //    PerformanceCounters.SessionFaulted(perfCounterId);
        }

        protected override void OnMessageDropped()
        {
            //if (PerformanceCounters.PerformanceCountersEnabled)
            //    PerformanceCounters.MessageDropped(perfCounterId);
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

        protected override async Task ProcessMessageAsync(WsrmMessageInfo info)
        {
             if (!await ReliableSession.ProcessInfoAsync(info, null))
                return;

            if (!ReliableSession.VerifyDuplexProtocolElements(info, null))
                return;

            if (info.CreateSequenceInfo != null)
            {
                EndpointAddress acksTo;

                if (WsrmUtilities.ValidateCreateSequence(info, serviceDispatcher, Binder.Channel, out acksTo))
                {
                    Message response = WsrmUtilities.CreateCreateSequenceResponse(Settings.MessageVersion,
                        Settings.ReliableMessagingVersion, true, info.CreateSequenceInfo, Settings.Ordered,
                        ReliableSession.InputID, acksTo);
                    using (info.Message)
                    {
                        using (response)
                        {
                            if (((IServerReliableChannelBinder)Binder).AddressResponse(info.Message, response))
                            {
                                await Binder.SendAsync(response, TimeoutHelper.GetCancellationToken(DefaultSendTimeout)); ;
                            }
                        }
                    }
                }
                else
                {
                    ReliableSession.OnLocalFault(info.FaultException, info.FaultReply, null);
                }

                return;
            }

            await ProcessDuplexMessageAsync(info);
        }

        private class DuplexServerReliableSession : ServerReliableSession, IDuplexSession
        {
            private readonly ServerReliableDuplexSessionChannel _channel;

            public DuplexServerReliableSession(ServerReliableDuplexSessionChannel channel,
                ReliableServiceDispatcherBase<IDuplexSessionChannel> listener, FaultHelper faultHelper, UniqueId inputID,
                UniqueId outputID)
                : base(channel, listener, (IServerReliableChannelBinder)channel.Binder, faultHelper, inputID, outputID)
            {
                _channel = channel;
            }

            public Task CloseOutputSessionAsync()
            {
                return CloseOutputSessionAsync(TimeoutHelper.GetCancellationToken(_channel.DefaultCloseTimeout));
            }

            public Task CloseOutputSessionAsync(CancellationToken token)
            {
                return _channel.OnCloseOutputSessionAsync(token);
            }
        }
    }
}
