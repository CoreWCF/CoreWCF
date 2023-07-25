// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;
using CoreWCF.Security;
using static CoreWCF.Runtime.TaskHelpers;

namespace CoreWCF.Channels
{
    internal delegate Task AsyncOperationWithCancellationCallback(CancellationToken token);

    internal sealed class Guard
    {
        private readonly TaskCompletionSource<object> _tcs;
        private readonly int _currentCount = 0;
        private readonly int _maxCount;
        private bool _closed;
        private readonly object _thisLock = new object();
        private readonly SemaphoreSlim _semaphore;

        public Guard() : this(1) { }

        public Guard(int maxCount)
        {
            _semaphore = new SemaphoreSlim(maxCount, maxCount);
            _maxCount = maxCount;
        }

        public void Abort()
        {
            _closed = true;
        }

        public async Task CloseAsync(CancellationToken token)  
        {
            try
            {
                for (int i = 0; i < _maxCount; i++)
                {
                    await _semaphore.WaitAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(SR.Format(SR.TimeoutOnOperation, TimeoutHelper.GetOriginalTimeout(token))));
            }
            finally
            {
                _semaphore.Dispose();
            }
        }

        public Task<bool> EnterAsync()
        {
            if (_closed)
                return Task.FromResult(false);

            return _semaphore.WaitAsync(0);
        }

        public void Exit()
        {
            try
            {
                _semaphore.Release();
            }
            catch (SemaphoreFullException)
            {
                throw Fx.AssertAndThrow("Exit can only be called after Enter.");
            }
        }
    }

    internal class InterruptibleTimer
    {
        private readonly WaitCallback _callback;
        private bool _aborted = false;
        private TimeSpan _defaultInterval;
        private static readonly Action<object> s_onTimerElapsed = new Action<object>(OnTimerElapsed);
        private bool _set = false;
        private readonly object _state;
        private readonly object _thisLock = new object();
        private IOThreadTimer _timer;

        public InterruptibleTimer(TimeSpan defaultInterval, WaitCallback callback, object state)
        {
            if (callback == null)
            {
                throw Fx.AssertAndThrow("Argument callback cannot be null.");
            }

            _defaultInterval = defaultInterval;
            _callback = callback;
            _state = state;
        }

        private object ThisLock => _thisLock;

        public void Abort()
        {
            lock (ThisLock)
            {
                _aborted = true;

                if (_set)
                {
                    _timer.Cancel();
                    _set = false;
                }
            }
        }

        public bool Cancel()
        {
            lock (ThisLock)
            {
                if (_aborted)
                {
                    return false;
                }

                if (_set)
                {
                    _timer.Cancel();
                    _set = false;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        private void OnTimerElapsed()
        {
            lock (ThisLock)
            {
                if (_aborted)
                    return;

                _set = false;
            }

            _callback(_state);
        }

        private static void OnTimerElapsed(object state)
        {
            InterruptibleTimer interruptibleTimer = (InterruptibleTimer)state;
            interruptibleTimer.OnTimerElapsed();
        }

        public void Set()
        {
            Set(_defaultInterval);
        }

        public void Set(TimeSpan interval)
        {
            InternalSet(interval, false);
        }

        public void SetIfNotSet()
        {
            InternalSet(_defaultInterval, true);
        }

        private void InternalSet(TimeSpan interval, bool ifNotSet)
        {
            lock (ThisLock)
            {
                if (_aborted || (ifNotSet && _set))
                    return;

                if (_timer == null)
                    _timer = new IOThreadTimer(s_onTimerElapsed, this, true);

                _timer.Set(interval);
                _set = true;
            }
        }
    }

    internal class InterruptibleWaitObject
    {
        private bool _aborted = false;
        private CommunicationObject _communicationObject;
        private bool _set;
        private int _syncWaiters;
        private readonly object _thisLock = new object();
        private readonly bool _throwTimeoutByDefault = true;
        private TaskCompletionSource<object> _tcs;

        public InterruptibleWaitObject(bool signaled)
            : this(signaled, true)
        {
        }

        public InterruptibleWaitObject(bool signaled, bool throwTimeoutByDefault)
        {
            _set = signaled;
            _throwTimeoutByDefault = throwTimeoutByDefault;
        }

        public void Abort(CommunicationObject communicationObject)
        {
            if (communicationObject == null)
            {
                throw Fx.AssertAndThrow("Argument communicationObject cannot be null.");
            }

            lock (_thisLock)
            {
                if (_aborted)
                {
                    return;
                }

                _communicationObject = communicationObject;

                _aborted = true;
                InternalSet();
            }
        }

        public void Fault(CommunicationObject communicationObject)
        {
            if (communicationObject == null)
            {
                throw Fx.AssertAndThrow("Argument communicationObject cannot be null.");
            }

            lock (_thisLock)
            {
                if (_aborted)
                {
                    return;
                }

                _communicationObject = communicationObject;

                _aborted = false;
                InternalSet();
            }
        }

        private Exception GetException()
        {
            if (_communicationObject == null)
            {
                Fx.Assert("Caller is attempting to retrieve an exception from a null communicationObject.");
            }

            return _aborted
                ? _communicationObject.CreateAbortedException()
                : _communicationObject.GetPendingException();
        }

        private void InternalSet()
        {
            lock (_thisLock)
            {
                _set = true;

                if (_tcs != null)
                {
                    _tcs.TrySetResult(null);
                }
            }
        }

        public void Reset()
        {
            lock (_thisLock)
            {
                _communicationObject = null;
                _aborted = false;
                _set = false;

                if (_tcs != null && _tcs.Task.IsCompleted)
                {
                    _tcs = new TaskCompletionSource<object>();
                }
            }
        }

        public void Set()
        {
            InternalSet();
        }

        public Task<bool> WaitAsync(CancellationToken token)
        {
            return WaitAsync(token, _throwTimeoutByDefault);
        }

        public async Task<bool> WaitAsync(CancellationToken token, bool throwTimeoutException)
        {
            lock (_thisLock)
            {
                if (_set)
                {
                    if (_communicationObject != null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(GetException());
                    }

                    return true;
                }

                if (_tcs == null)
                {
                    _tcs = new TaskCompletionSource<object>();
                }

                _syncWaiters++;
            }

            try
            {
                if (!await _tcs.Task.WaitWithCancellationAsync(token))
                {
                    if (throwTimeoutException)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(SR.Format(SR.TimeoutOnOperation, TimeoutHelper.GetOriginalTimeout(token))));
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            finally
            {
                lock (_thisLock)
                {
                    // Last one out turns off the light.
                    _syncWaiters--;
                    if (_syncWaiters == 0 && _tcs.Task.IsCompleted)
                    {
                        _tcs = null;
                    }
                }
            }

            if (_communicationObject != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(GetException());
            }

            return true;
        }
    }

    internal static class ReliableMessagingConstants
    {
        static public TimeSpan UnknownInitiationTime = TimeSpan.FromSeconds(2);
        static public TimeSpan RequestorIterationTime = TimeSpan.FromSeconds(10);
        static public TimeSpan RequestorReceiveTime = TimeSpan.FromSeconds(10);
        static public int MaxSequenceRanges = 128;
    }

    // This class and its derivates attempt to unify 3 similar request reply patterns.
    // 1. Straightforward R/R pattern
    // 2. R/R pattern with binder and exception semantics on Open (CreateSequence)
    // 3. TerminateSequence request - TerminateSequence response for R(Request|Reply)SC
    internal abstract class ReliableRequestor
    {
        private readonly InterruptibleWaitObject _abortHandle = new InterruptibleWaitObject(false, false);
        private TimeSpan _originalTimeout;

        public IReliableChannelBinder Binder { protected get; set; }

        public bool IsCreateSequence { protected get; set; }

        public ActionHeader MessageAction { private get; set; }

        public BodyWriter MessageBody { private get; set; }

        public UniqueId MessageId { get; private set; }

        public WsrmMessageHeader MessageHeader { get; set; }

        public MessageVersion MessageVersion { private get; set; }

        public string TimeoutString1Index { private get; set; }

        public void Abort(CommunicationObject communicationObject) => _abortHandle.Abort(communicationObject);

        private Message CreateRequestMessage()
        {
            Message request = Message.CreateMessage(MessageVersion, MessageAction, MessageBody);
            request.Properties.AllowOutputBatching = false;

            if (MessageHeader != null)
            {
                request.Headers.Insert(0, MessageHeader);
            }

            if (MessageId != null)
            {
                request.Headers.MessageId = MessageId;
                RequestReplyCorrelator.PrepareRequest(request);

                EndpointAddress address = Binder.LocalAddress;

                if (address == null)
                {
                    request.Headers.ReplyTo = null;
                }
                else if (MessageVersion.Addressing == AddressingVersion.WSAddressingAugust2004)
                {
                    request.Headers.ReplyTo = address;
                }
                else if (MessageVersion.Addressing == AddressingVersion.WSAddressing10)
                {
                    request.Headers.ReplyTo = address.IsAnonymous ? null : address;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ProtocolException(SR.Format(SR.AddressingVersionNotSupported, MessageVersion.Addressing)));
                }
            }

            return request;
        }

        public virtual void Fault(CommunicationObject communicationObject) => _abortHandle.Fault(communicationObject);

        public abstract WsrmMessageInfo GetInfo();

        private CancellationToken GetNextRequestCancellationToken(CancellationToken token, out bool lastIteration)
        {
            // Because we don't have the actual timeout value, we can't indicate it's the last iteration when
            // the overall timeout is iminent. The best we can do is to allow one last request after the overall
            // CancellationToken has now been cancelled. This means in a timing out scenario, on average we will
            // take RequestorIterationTime / 2 extra time than was intended. As RequestorIterationTime is 10 seconds,
            // this means when there's a problem, we will potentially go over time by an average of 5 seconds when
            // retrying. This is low enough that it shouldn't be consequential.
            lastIteration = token.IsCancellationRequested;
            CancellationToken iterationToken = TimeoutHelper.GetCancellationToken(ReliableMessagingConstants.RequestorIterationTime);
            return iterationToken;
        }

        private bool HandleException(Exception exception, bool lastIteration)
        {
            if (IsCreateSequence)
            {
                if (exception is QuotaExceededException)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new CommunicationException(exception.Message, exception));
                }

                if (!Binder.IsHandleable(exception)
                    || exception is MessageSecurityException
                    || exception is SecurityNegotiationException
                    || (Binder.State != CommunicationState.Opened)
                    || lastIteration)
                {
                    return false;
                }

                return true;
            }
            else
            {
                return Binder.IsHandleable(exception);
            }
        }

        private void ThrowTimeoutException()
        {
            if (TimeoutString1Index != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new TimeoutException(SR.Format(TimeoutString1Index, _originalTimeout)));
            }
        }

        protected abstract Task<Message> OnRequestAsync(Message request, CancellationToken token, bool last);

        public async Task<Message> RequestAsync(CancellationToken token)
        {
            TimeoutHelper iterationTimeoutHelper;
            bool lastIteration;

            while (true)
            {
                Message request = null;
                Message reply = null;
                bool requestCompleted = false;
                CancellationToken requestCancellationToken = GetNextRequestCancellationToken(token, out lastIteration);

                try
                {
                    request = CreateRequestMessage();
                    reply = await OnRequestAsync(request, requestCancellationToken, lastIteration);
                    requestCompleted = true;
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e) || !HandleException(e, lastIteration))
                    {
                        throw;
                    }

                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                finally
                {
                    if (request != null)
                    {
                        request.Close();
                    }
                }

                if (requestCompleted)
                {
                    if (ValidateReply(reply))
                    {
                        return reply;
                    }
                }

                if (lastIteration)
                    break;

                await _abortHandle.WaitAsync(requestCancellationToken);
            }

            ThrowTimeoutException();
            return null;
        }

        public abstract void SetInfo(WsrmMessageInfo info);

        public void SetRequestResponsePattern()
        {
            if (MessageId != null)
            {
                throw Fx.AssertAndThrow("Initialize messageId only once.");
            }

            MessageId = new UniqueId();
        }

        private bool ValidateReply(Message response)
        {
            if (MessageId != null)
            {
                // r/r pattern requires a response
                return response != null;
            }
            else
            {
                return true;
            }
        }
    }

    internal sealed class SendWaitReliableRequestor : ReliableRequestor
    {
        private bool _replied = false;
        private readonly InterruptibleWaitObject _replyHandle = new InterruptibleWaitObject(false, true);
        private WsrmMessageInfo _replyInfo;
        private Message _request;
        private readonly object _thisLock = new object();

        private object ThisLock => _thisLock;

        public override void Fault(CommunicationObject communicationObject)
        {
            _replied = true;
            _replyHandle.Fault(communicationObject);
            base.Fault(communicationObject);
        }

        public override WsrmMessageInfo GetInfo()
        {
            return _replyInfo;
        }

        private Message GetReply(bool last)
        {
            lock (ThisLock)
            {
                if (_replyInfo != null)
                {
                    _replied = true;
                    return _replyInfo.Message;
                }
                else if (last)
                {
                    _replied = true;
                }
            }

            return null;
        }

        private CancellationToken GetWaitCancellationToken()
        {
            return TimeoutHelper.GetCancellationToken(ReliableMessagingConstants.RequestorReceiveTime);
        }

        protected override async Task<Message> OnRequestAsync(Message request, CancellationToken token, bool last)
        {
            await Binder.SendAsync(request, token, MaskingMode.None);
            CancellationToken waitToken = GetWaitCancellationToken();
            using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, waitToken))
            {
                await _replyHandle.WaitAsync(linkedCts.Token);
            }

            return GetReply(last);
        }

        public override void SetInfo(WsrmMessageInfo info)
        {
            lock (ThisLock)
            {
                if (_replied || _replyInfo != null)
                {
                    return;
                }

                _replyInfo = info;
            }

            _replyHandle.Set();
        }
    }

    internal static class WsrmUtilities
    {
        public static TimeSpan CalculateKeepAliveInterval(TimeSpan inactivityTimeout, int maxRetryCount)
        {
            return Ticks.ToTimeSpan(Ticks.FromTimeSpan(inactivityTimeout) / 2 / maxRetryCount);
        }

        internal static UniqueId NextSequenceId()
        {
            return new UniqueId();
        }

        internal static void AddAcknowledgementHeader(ReliableMessagingVersion reliableMessagingVersion,
            Message message, UniqueId id, SequenceRangeCollection ranges, bool final)
        {
            AddAcknowledgementHeader(reliableMessagingVersion, message, id, ranges, final, -1);
        }

        internal static void AddAcknowledgementHeader(ReliableMessagingVersion reliableMessagingVersion,
            Message message, UniqueId id, SequenceRangeCollection ranges, bool final, int bufferRemaining)
        {
            message.Headers.Insert(0,
                new WsrmAcknowledgmentHeader(reliableMessagingVersion, id, ranges, final, bufferRemaining));
        }

        internal static void AddAckRequestedHeader(ReliableMessagingVersion reliableMessagingVersion, Message message,
            UniqueId id)
        {
            message.Headers.Insert(0, new WsrmAckRequestedHeader(reliableMessagingVersion, id));
        }

        internal static void AddSequenceHeader(ReliableMessagingVersion reliableMessagingVersion, Message message,
            UniqueId id, Int64 sequenceNumber, bool isLast)
        {
            message.Headers.Insert(0,
                new WsrmSequencedMessageHeader(reliableMessagingVersion, id, sequenceNumber, isLast));
        }

        internal static void AssertWsrm11(ReliableMessagingVersion reliableMessagingVersion)
        {
            if (reliableMessagingVersion != ReliableMessagingVersion.WSReliableMessaging11)
            {
                throw Fx.AssertAndThrow("WS-ReliableMessaging 1.1 required.");
            }
        }

        internal static Message CreateAcknowledgmentMessage(MessageVersion version,
            ReliableMessagingVersion reliableMessagingVersion, UniqueId id, SequenceRangeCollection ranges, bool final,
            int bufferRemaining)
        {
            Message message = Message.CreateMessage(version,
                WsrmIndex.GetSequenceAcknowledgementActionHeader(version.Addressing, reliableMessagingVersion));

            AddAcknowledgementHeader(reliableMessagingVersion, message, id, ranges, final,
                bufferRemaining);
            message.Properties.AllowOutputBatching = false;

            return message;
        }

        internal static Message CreateAckRequestedMessage(MessageVersion messageVersion,
            ReliableMessagingVersion reliableMessagingVersion, UniqueId id)
        {
            Message message = Message.CreateMessage(messageVersion,
                WsrmIndex.GetAckRequestedActionHeader(messageVersion.Addressing, reliableMessagingVersion));

            AddAckRequestedHeader(reliableMessagingVersion, message, id);
            message.Properties.AllowOutputBatching = false;

            return message;
        }

        internal static Message CreateCloseSequenceResponse(MessageVersion messageVersion, UniqueId messageId,
            UniqueId inputId)
        {
            CloseSequenceResponse response = new CloseSequenceResponse(inputId);

            Message message = Message.CreateMessage(messageVersion,
                WsrmIndex.GetCloseSequenceResponseActionHeader(messageVersion.Addressing), response);

            message.Headers.RelatesTo = messageId;
            return message;
        }

        internal static Message CreateCreateSequenceResponse(MessageVersion messageVersion,
            ReliableMessagingVersion reliableMessagingVersion, bool duplex, CreateSequenceInfo createSequenceInfo,
            bool ordered, UniqueId inputId, EndpointAddress acceptAcksTo)
        {
            CreateSequenceResponse response = new CreateSequenceResponse(messageVersion.Addressing, reliableMessagingVersion);
            response.Identifier = inputId;
            response.Expires = createSequenceInfo.Expires;
            response.Ordered = ordered;

            if (duplex)
                response.AcceptAcksTo = acceptAcksTo;

            Message responseMessage
                = Message.CreateMessage(messageVersion, ActionHeader.Create(
                WsrmIndex.GetCreateSequenceResponseAction(reliableMessagingVersion), messageVersion.Addressing), response);

            return responseMessage;
        }

        internal static Message CreateCSRefusedCommunicationFault(MessageVersion messageVersion,
            ReliableMessagingVersion reliableMessagingVersion, string reason)
        {
            return CreateCSRefusedFault(messageVersion, reliableMessagingVersion, false, null, reason);
        }

        internal static Message CreateCSRefusedProtocolFault(MessageVersion messageVersion,
            ReliableMessagingVersion reliableMessagingVersion, string reason)
        {
            return CreateCSRefusedFault(messageVersion, reliableMessagingVersion, true, null, reason);
        }

        internal static Message CreateCSRefusedServerTooBusyFault(MessageVersion messageVersion,
            ReliableMessagingVersion reliableMessagingVersion, string reason)
        {
            FaultCode subCode = new FaultCode(WsrmFeb2005Strings.ConnectionLimitReached,
                WsrmFeb2005Strings.NETNamespace);
            subCode = new FaultCode(WsrmFeb2005Strings.CreateSequenceRefused,
                WsrmIndex.GetNamespaceString(reliableMessagingVersion), subCode);
            return CreateCSRefusedFault(messageVersion, reliableMessagingVersion, false, subCode, reason);
        }

        private static Message CreateCSRefusedFault(MessageVersion messageVersion,
            ReliableMessagingVersion reliableMessagingVersion, bool isSenderFault, FaultCode subCode, string reason)
        {
            FaultCode code;

            if (messageVersion.Envelope == EnvelopeVersion.Soap11)
            {
                code = new FaultCode(WsrmFeb2005Strings.CreateSequenceRefused, WsrmIndex.GetNamespaceString(reliableMessagingVersion));
            }
            else if (messageVersion.Envelope == EnvelopeVersion.Soap12)
            {
                if (subCode == null)
                    subCode = new FaultCode(WsrmFeb2005Strings.CreateSequenceRefused, WsrmIndex.GetNamespaceString(reliableMessagingVersion), subCode);

                if (isSenderFault)
                    code = FaultCode.CreateSenderFaultCode(subCode);
                else
                    code = FaultCode.CreateReceiverFaultCode(subCode);
            }
            else
            {
                throw Fx.AssertAndThrow("Unsupported version.");
            }

            FaultReason faultReason = new FaultReason(SR.Format(SR.CSRefused, reason), CultureInfo.CurrentCulture);

            MessageFault fault = MessageFault.CreateFault(code, faultReason);
            string action = WsrmIndex.GetFaultActionString(messageVersion.Addressing, reliableMessagingVersion);
            return Message.CreateMessage(messageVersion, fault, action);
        }

        public static Exception CreateCSFaultException(MessageVersion version,
            ReliableMessagingVersion reliableMessagingVersion, Message message, IChannel innerChannel)
        {
            MessageFault fault = MessageFault.CreateFault(message, TransportDefaults.MaxRMFaultSize);
            FaultCode code = fault.Code;
            FaultCode subCode;

            if (version.Envelope == EnvelopeVersion.Soap11)
            {
                subCode = code;
            }
            else if (version.Envelope == EnvelopeVersion.Soap12)
            {
                subCode = code.SubCode;
            }
            else
            {
                throw Fx.AssertAndThrow("Unsupported version.");
            }

            if (subCode != null)
            {
                // CreateSequenceRefused
                if ((subCode.Namespace == WsrmIndex.GetNamespaceString(reliableMessagingVersion))
                    && (subCode.Name == WsrmFeb2005Strings.CreateSequenceRefused))
                {
                    string reason = FaultException.GetSafeReasonText(fault);

                    if (version.Envelope == EnvelopeVersion.Soap12)
                    {
                        FaultCode subSubCode = subCode.SubCode;
                        if ((subSubCode != null)
                            && (subSubCode.Namespace == WsrmFeb2005Strings.NETNamespace)
                            && (subSubCode.Name == WsrmFeb2005Strings.ConnectionLimitReached))
                        {
                            return new ServerTooBusyException(reason);
                        }

                        if (code.IsSenderFault)
                        {
                            return new ProtocolException(reason);
                        }
                    }

                    return new CommunicationException(reason);
                }
                else if ((subCode.Namespace == version.Addressing.Namespace)
                    && (subCode.Name == AddressingStrings.EndpointUnavailable))
                {
                    return new EndpointNotFoundException(FaultException.GetSafeReasonText(fault));
                }
            }

            FaultConverter faultConverter = innerChannel.GetProperty<FaultConverter>();
            if (faultConverter == null)
                faultConverter = FaultConverter.GetDefaultFaultConverter(version);

            Exception exception;
            if (faultConverter.TryCreateException(message, fault, out exception))
            {
                return exception;
            }
            else
            {
                return new ProtocolException(SR.Format(SR.UnrecognizedFaultReceivedOnOpen, fault.Code.Namespace, fault.Code.Name, FaultException.GetSafeReasonText(fault)));
            }
        }

        internal static Message CreateEndpointNotFoundFault(MessageVersion version, string reason)
        {
            FaultCode subCode = new FaultCode(AddressingStrings.EndpointUnavailable, version.Addressing.Namespace);
            FaultCode code;

            if (version.Envelope == EnvelopeVersion.Soap11)
            {
                code = subCode;
            }
            else if (version.Envelope == EnvelopeVersion.Soap12)
            {
                code = FaultCode.CreateSenderFaultCode(subCode);
            }
            else
            {
                throw Fx.AssertAndThrow("Unsupported version.");
            }

            FaultReason faultReason = new FaultReason(reason, CultureInfo.CurrentCulture);
            MessageFault fault = MessageFault.CreateFault(code, faultReason);
            return Message.CreateMessage(version, fault, version.Addressing.DefaultFaultAction);
        }

        internal static Message CreateTerminateMessage(MessageVersion version,
            ReliableMessagingVersion reliableMessagingVersion, UniqueId id)
        {
            return CreateTerminateMessage(version, reliableMessagingVersion, id, -1);
        }

        internal static Message CreateTerminateMessage(MessageVersion version,
            ReliableMessagingVersion reliableMessagingVersion, UniqueId id, Int64 last)
        {
            Message message = Message.CreateMessage(version,
                WsrmIndex.GetTerminateSequenceActionHeader(version.Addressing, reliableMessagingVersion),
                new TerminateSequence(reliableMessagingVersion, id, last));

            message.Properties.AllowOutputBatching = false;

            return message;
        }

        internal static Message CreateTerminateResponseMessage(MessageVersion version, UniqueId messageId, UniqueId sequenceId)
        {
            Message message = Message.CreateMessage(version,
                WsrmIndex.GetTerminateSequenceResponseActionHeader(version.Addressing),
                new TerminateSequenceResponse(sequenceId));

            message.Properties.AllowOutputBatching = false;
            message.Headers.RelatesTo = messageId;
            return message;
        }

        internal static UniqueId GetInputId(WsrmMessageInfo info)
        {
            if (info.TerminateSequenceInfo != null)
            {
                return info.TerminateSequenceInfo.Identifier;
            }

            if (info.SequencedMessageInfo != null)
            {
                return info.SequencedMessageInfo.SequenceID;
            }

            if (info.AckRequestedInfo != null)
            {
                return info.AckRequestedInfo.SequenceID;
            }

            if (info.WsrmHeaderFault != null && info.WsrmHeaderFault.FaultsInput)
            {
                return info.WsrmHeaderFault.SequenceID;
            }

            if (info.CloseSequenceInfo != null)
            {
                return info.CloseSequenceInfo.Identifier;
            }

            return null;
        }

        internal static UniqueId GetOutputId(ReliableMessagingVersion reliableMessagingVersion, WsrmMessageInfo info)
        {
            if (info.AcknowledgementInfo != null)
            {
                return info.AcknowledgementInfo.SequenceID;
            }

            if (info.WsrmHeaderFault != null && info.WsrmHeaderFault.FaultsOutput)
            {
                return info.WsrmHeaderFault.SequenceID;
            }

            if (info.TerminateSequenceResponseInfo != null)
            {
                return info.TerminateSequenceResponseInfo.Identifier;
            }

            if (reliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
            {
                if (info.CloseSequenceInfo != null)
                {
                    return info.CloseSequenceInfo.Identifier;
                }

                if (info.CloseSequenceResponseInfo != null)
                {
                    return info.CloseSequenceResponseInfo.Identifier;
                }

                if (info.TerminateSequenceResponseInfo != null)
                {
                    return info.TerminateSequenceResponseInfo.Identifier;
                }
            }

            return null;
        }

        internal static bool IsWsrmAction(ReliableMessagingVersion reliableMessagingVersion, string action)
        {
            if (action == null)
                return false;
            return (action.StartsWith(WsrmIndex.GetNamespaceString(reliableMessagingVersion), StringComparison.Ordinal));
        }

        public static void ReadEmptyElement(XmlDictionaryReader reader)
        {
            if (reader.IsEmptyElement)
            {
                reader.Read();
            }
            else
            {
                reader.Read();
                reader.ReadEndElement();
            }
        }

        public static UniqueId ReadIdentifier(XmlDictionaryReader reader,
            ReliableMessagingVersion reliableMessagingVersion)
        {
            reader.ReadStartElement(XD.WsrmFeb2005Dictionary.Identifier, WsrmIndex.GetNamespace(reliableMessagingVersion));
            UniqueId sequenceID = reader.ReadContentAsUniqueId();
            reader.ReadEndElement();
            return sequenceID;
        }

        public static Int64 ReadSequenceNumber(XmlDictionaryReader reader)
        {
            return ReadSequenceNumber(reader, false);
        }

        public static Int64 ReadSequenceNumber(XmlDictionaryReader reader, bool allowZero)
        {
            Int64 sequenceNumber = reader.ReadContentAsLong();

            if (sequenceNumber < 0 || (sequenceNumber == 0 && !allowZero))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(
                    SR.Format(SR.InvalidSequenceNumber, sequenceNumber)));
            }

            return sequenceNumber;
        }

        // Caller owns message.
        public static WsrmFault ValidateCloseSequenceResponse(ChannelReliableSession session, UniqueId messageId,
            WsrmMessageInfo info, Int64 last)
        {
            string exceptionString;
            string faultString;
            if (info.CloseSequenceResponseInfo == null)
            {
                exceptionString = SR.Format(SR.InvalidWsrmResponseSessionFaultedExceptionString,
                    Wsrm11Strings.CloseSequence, info.Action,
                    Wsrm11Strings.CloseSequenceResponseAction);
                faultString = SR.Format(SR.InvalidWsrmResponseSessionFaultedFaultString,
                    Wsrm11Strings.CloseSequence, info.Action,
                    Wsrm11Strings.CloseSequenceResponseAction);
            }
            else if (!Equals(messageId, info.CloseSequenceResponseInfo.RelatesTo))
            {
                exceptionString = SR.Format(SR.WsrmMessageWithWrongRelatesToExceptionString, Wsrm11Strings.CloseSequence);
                faultString = SR.Format(SR.WsrmMessageWithWrongRelatesToFaultString, Wsrm11Strings.CloseSequence);
            }
            else if (info.AcknowledgementInfo == null || !info.AcknowledgementInfo.Final)
            {
                exceptionString = SR.MissingFinalAckExceptionString;
                faultString = SR.SequenceTerminatedMissingFinalAck;
            }
            else
            {
                return ValidateFinalAck(session, info, last);
            }

            UniqueId sequenceId = session.OutputID;
            return SequenceTerminatedFault.CreateProtocolFault(sequenceId, faultString, exceptionString);
        }

        public static bool ValidateCreateSequence<TChannel>(WsrmMessageInfo info,
            ReliableServiceDispatcherBase<TChannel> serviceDispatcher, IChannel channel, out EndpointAddress acksTo)
            where TChannel : class, IChannel
        {
            acksTo = null;
            string reason = null;

            if (info.CreateSequenceInfo.OfferIdentifier == null)
            {
                if (typeof(TChannel) == typeof(IDuplexSessionChannel))
                    reason = SR.Format(SR.CSRefusedDuplexNoOffer, serviceDispatcher.Uri);
                else if (typeof(TChannel) == typeof(IReplySessionChannel))
                    reason = SR.Format(SR.CSRefusedReplyNoOffer, serviceDispatcher.Uri);
            }
            else if (serviceDispatcher.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
            {
                if (typeof(TChannel) == typeof(IInputSessionChannel))
                    reason = SR.Format(SR.CSRefusedInputOffer, serviceDispatcher.Uri);
            }

            if (reason != null)
            {
                info.FaultReply = CreateCSRefusedProtocolFault(serviceDispatcher.MessageVersion,
                    serviceDispatcher.ReliableMessagingVersion, reason);
                info.FaultException = new ProtocolException(SR.ConflictingOffer);
                return false;
            }

            if (serviceDispatcher.LocalAddresses != null)
            {
                Collection<EndpointAddress> addresses = new Collection<EndpointAddress>();

                try
                {
                    serviceDispatcher.LocalAddresses.GetMatchingValues(info.Message, addresses);
                }
                catch (CommunicationException e)
                {
                    FaultConverter converter = channel.GetProperty<FaultConverter>();
                    if (converter == null)
                        converter = FaultConverter.GetDefaultFaultConverter(serviceDispatcher.MessageVersion);

                    Message faultReply;
                    if (converter.TryCreateFaultMessage(e, out faultReply))
                    {
                        info.FaultReply = faultReply;
                        info.FaultException = new ProtocolException(SR.MessageExceptionOccurred, e);
                        return false;
                    }

                    throw;
                }

                if (addresses.Count > 0)
                {
                    EndpointAddress match = addresses[0];
                    acksTo = new EndpointAddress(info.CreateSequenceInfo.To, match.Identity, match.Headers);
                    return true;
                }
                else
                {
                    info.FaultReply = CreateEndpointNotFoundFault(serviceDispatcher.MessageVersion, SR.Format(SR.EndpointNotFound, info.CreateSequenceInfo.To));
                    info.FaultException = new ProtocolException(SR.ConflictingAddress);
                    return false;
                }
            }
            else
            {
                acksTo = new EndpointAddress(info.CreateSequenceInfo.To);
                return true;
            }
        }

        public static WsrmFault ValidateFinalAck(ChannelReliableSession session, WsrmMessageInfo info, Int64 last)
        {
            WsrmAcknowledgmentInfo ackInfo = info.AcknowledgementInfo;
            WsrmFault fault = ValidateFinalAckExists(session, ackInfo);

            if (fault != null)
            {
                return fault;
            }

            SequenceRangeCollection finalRanges = ackInfo.Ranges;

            if (last == 0)
            {
                if (finalRanges.Count == 0)
                {
                    return null;
                }
            }
            else
            {
                if ((finalRanges.Count == 1) && (finalRanges[0].Lower == 1) && (finalRanges[0].Upper == last))
                {
                    return null;
                }
            }

            return new InvalidAcknowledgementFault(session.OutputID, ackInfo.Ranges);
        }

        public static WsrmFault ValidateFinalAckExists(ChannelReliableSession session, WsrmAcknowledgmentInfo ackInfo)
        {
            if (ackInfo == null || !ackInfo.Final)
            {
                string exceptionString = SR.MissingFinalAckExceptionString;
                string faultString = SR.SequenceTerminatedMissingFinalAck;
                return SequenceTerminatedFault.CreateProtocolFault(session.OutputID, faultString, exceptionString);
            }

            return null;
        }

        // Caller owns message.
        public static WsrmFault ValidateTerminateSequenceResponse(ChannelReliableSession session, UniqueId messageId,
            WsrmMessageInfo info, Int64 last)
        {
            string exceptionString;
            string faultString;
            if (info.WsrmHeaderFault is UnknownSequenceFault)
            {
                return null;
            }
            else if (info.TerminateSequenceResponseInfo == null)
            {
                exceptionString = SR.Format(SR.InvalidWsrmResponseSessionFaultedExceptionString,
                    WsrmFeb2005Strings.TerminateSequence, info.Action,
                    Wsrm11Strings.TerminateSequenceResponseAction);
                faultString = SR.Format(SR.InvalidWsrmResponseSessionFaultedFaultString,
                    WsrmFeb2005Strings.TerminateSequence, info.Action,
                    Wsrm11Strings.TerminateSequenceResponseAction);
            }
            else if (!Equals(messageId, info.TerminateSequenceResponseInfo.RelatesTo))
            {
                exceptionString = SR.Format(SR.WsrmMessageWithWrongRelatesToExceptionString, WsrmFeb2005Strings.TerminateSequence);
                faultString = SR.Format(SR.WsrmMessageWithWrongRelatesToFaultString, WsrmFeb2005Strings.TerminateSequence);
            }
            else
            {
                return ValidateFinalAck(session, info, last);
            }

            UniqueId sequenceId = session.OutputID;
            return SequenceTerminatedFault.CreateProtocolFault(sequenceId, faultString, exceptionString);
        }

        // Checks that ReplyTo and RemoteAddress are equivalent. Will fault the session with SequenceTerminatedFault.
        // Meant to be used for CloseSequence and TerminateSequenceAsync in Wsrm 1.1.
        public static bool ValidateWsrmRequest(ChannelReliableSession session, WsrmRequestInfo info,
            IReliableChannelBinder binder, RequestContext context)
        {
            if (!(info is CloseSequenceInfo) && !(info is TerminateSequenceInfo))
            {
                throw Fx.AssertAndThrow("Method is meant for CloseSequence or TerminateSequenceAsync only.");
            }

            if (info.ReplyTo.Uri != binder.RemoteAddress.Uri)
            {
                string faultString = SR.Format(SR.WsrmRequestIncorrectReplyToFaultString, info.RequestName);
                string exceptionString = SR.Format(SR.WsrmRequestIncorrectReplyToExceptionString, info.RequestName);
                WsrmFault fault = SequenceTerminatedFault.CreateProtocolFault(session.InputID, faultString, exceptionString);
                session.OnLocalFault(fault.CreateException(), fault, context);
                return false;
            }
            else
            {
                return true;
            }
        }

        public static void WriteIdentifier(XmlDictionaryWriter writer,
            ReliableMessagingVersion reliableMessagingVersion, UniqueId sequenceId)
        {
            writer.WriteStartElement(WsrmFeb2005Strings.Prefix, XD.WsrmFeb2005Dictionary.Identifier,
                WsrmIndex.GetNamespace(reliableMessagingVersion));
            writer.WriteValue(sequenceId);
            writer.WriteEndElement();
        }

        // These are strings that are not actually used anywhere.
        // This method and resources strings can be deleted whenever the resource file can be changed.
        public static string UseStrings()
        {
            string s = SR.SupportedAddressingModeNotSupported;
            s = SR.SequenceTerminatedUnexpectedCloseSequence;
            s = SR.UnexpectedCloseSequence;
            s = SR.SequenceTerminatedUnsupportedTerminateSequence;
            return s;
        }
    }
}
