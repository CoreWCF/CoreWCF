// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class ChannelReliableSession : ISession
    {
        private readonly IReliableChannelBinder _binder;
        private bool _canSendFault = true;
        private readonly ServiceChannelBase _channel;
        private SessionFaultState _faulted = SessionFaultState.NotFaulted;
        private readonly FaultHelper _faultHelper;
        private SequenceRangeCollection _finalRanges;
        private Guard _guard = new Guard(int.MaxValue);
        private InterruptibleTimer _inactivityTimer;
        private TimeSpan _initiationTime;
        private UniqueId _inputID;
        private bool _isSessionClosed = false;
        private UniqueId _outputID;
        private RequestContext _replyFaultContext;
        private readonly IReliableFactorySettings _settings;
        private Message _terminatingFault;
        private readonly object _thisLock = new object();
        private UnblockChannelCloseHandler _unblockChannelCloseCallback;

        protected ChannelReliableSession(ServiceChannelBase channel, IReliableFactorySettings settings, IReliableChannelBinder binder, FaultHelper faultHelper)
        {
            _channel = channel;
            _settings = settings;
            _binder = binder;
            _faultHelper = faultHelper;
            _inactivityTimer = new InterruptibleTimer(_settings.InactivityTimeout, new WaitCallback(OnInactivityElapsed), null);
            _initiationTime = ReliableMessagingConstants.UnknownInitiationTime;
        }

        protected ServiceChannelBase Channel => _channel;
        protected Guard Guard => _guard;

        public string Id
        {
            get
            {
                UniqueId sequenceId = SequenceID;
                if (sequenceId == null)
                    return null;
                else
                    return sequenceId.ToString();
            }
        }

        public TimeSpan InitiationTime
        {
            get => _initiationTime;
            protected set => _initiationTime = value;
        }

        public UniqueId InputID
        {
            get => _inputID;
            protected set => _inputID = value;
        }

        protected FaultHelper FaultHelper => _faultHelper;

        public UniqueId OutputID
        {
            get => _outputID;
            protected set => _outputID = value;
        }

        public abstract UniqueId SequenceID { get; }
        public IReliableFactorySettings Settings => _settings;
        protected object ThisLock => _thisLock;

        public UnblockChannelCloseHandler UnblockChannelCloseCallback
        {
            set => _unblockChannelCloseCallback = value;
        }

        public virtual void Abort()
        {
            _guard.Abort();
            _inactivityTimer.Abort();

            // Try to send a fault.
            bool sendFault;
            lock (ThisLock)
            {
                // Faulted thread already cleaned up. No need to to anything more.
                if (_faulted == SessionFaultState.CleanedUp)
                    return;

                // Can only send a fault if the other side did not send one already.
                sendFault = _canSendFault && (_faulted != SessionFaultState.RemotelyFaulted);    // NotFaulted || LocallyFaulted
                _faulted = SessionFaultState.CleanedUp;
            }

            if (sendFault)
            {
                if ((_binder.State == CommunicationState.Opened)
                    && _binder.Connected)
                {
                    if (_terminatingFault == null)
                    {
                        UniqueId sequenceId = InputID ?? OutputID;
                        if (sequenceId != null)
                        {
                            WsrmFault fault = SequenceTerminatedFault.CreateCommunicationFault(sequenceId, SR.SequenceTerminatedOnAbort, null);
                            _terminatingFault = fault.CreateMessage(_settings.MessageVersion,
                                _settings.ReliableMessagingVersion);
                        }
                    }

                    if (_terminatingFault != null)
                    {
                        AddFinalRanges();
                        _faultHelper.SendFaultAsync(_binder, _replyFaultContext, _terminatingFault);
                        return;
                    }
                }
            }

            // Got here so the session did not actually send a fault, must clean up resources.
            if (_terminatingFault != null)
                _terminatingFault.Close();
            if (_replyFaultContext != null)
                _replyFaultContext.Abort();
            _binder.Abort();
        }

        private void AddFinalRanges()
        {
            // This relies on the assumption that acknowledgements can be piggybacked on sequence
            // faults for the converse sequence.
            if (_finalRanges != null)
            {
                WsrmUtilities.AddAcknowledgementHeader(_settings.ReliableMessagingVersion,
                    _terminatingFault, InputID, _finalRanges, true);
            }
        }

        public abstract Task OpenAsync(TimeSpan timeout);

        public virtual async Task CloseAsync(CancellationToken token)
        {
            await Guard.CloseAsync(token);
            _inactivityTimer.Abort();
        }

        // Corresponds to the state where the other side could have gone away already.
        public void CloseSession()
        {
            _isSessionClosed = true;
        }

        protected virtual void FaultCore()
        {
            //if (TD.ReliableSessionChannelFaultedIsEnabled())
            //{
            //    TD.ReliableSessionChannelFaulted(Id);
            //}

            _inactivityTimer.Abort();
        }

        public void OnLocalFault(Exception e, WsrmFault fault, RequestContext context)
        {
            Message faultMessage = (fault == null) ? null : fault.CreateMessage(_settings.MessageVersion,
                _settings.ReliableMessagingVersion);
            OnLocalFault(e, faultMessage, context);
        }

        public void OnLocalFault(Exception e, Message faultMessage, RequestContext context)
        {
            if (_channel.Aborted ||
                _channel.State == CommunicationState.Faulted ||
                _channel.State == CommunicationState.Closed)
            {
                if (faultMessage != null)
                    faultMessage.Close();
                if (context != null)
                    context.Abort();
                return;
            }

            lock (ThisLock)
            {
                if (_faulted != SessionFaultState.NotFaulted)
                    return;
                _faulted = SessionFaultState.LocallyFaulted;
                _terminatingFault = faultMessage;
                _replyFaultContext = context;
            }

            FaultCore();
            _channel.Fault(e);
            UnblockChannelIfNecessary();
        }

        public void OnRemoteFault(WsrmFault fault)
        {
            OnRemoteFault(WsrmFault.CreateException(fault));
        }

        public void OnRemoteFault(Exception e)
        {
            if (_channel.Aborted ||
                _channel.State == CommunicationState.Faulted ||
                _channel.State == CommunicationState.Closed)
            {
                return;
            }

            lock (ThisLock)
            {
                if (_faulted != SessionFaultState.NotFaulted)
                    return;
                _faulted = SessionFaultState.RemotelyFaulted;
            }

            FaultCore();
            _channel.Fault(e);
            UnblockChannelIfNecessary();
        }

        public virtual void OnFaulted()
        {
            FaultCore();

            // Try to send a fault.
            bool sendFault;
            lock (ThisLock)
            {
                // Channel was faulted without the session being told first (e.g. open throws).
                // The session does not know what fault to send so let abort send it if it can.
                if (_faulted == SessionFaultState.NotFaulted)
                    return;

                // Abort thread decided to clean up.
                if (_faulted == SessionFaultState.CleanedUp)
                    return;

                // Can only send a fault if the other side did not send one already.
                sendFault = _canSendFault && (_faulted != SessionFaultState.RemotelyFaulted);  // LocallyFaulted
                _faulted = SessionFaultState.CleanedUp;
            }

            if (sendFault)
            {
                if ((_binder.State == CommunicationState.Opened)
                    && _binder.Connected
                    && (_terminatingFault != null))
                {
                    AddFinalRanges();
                    _faultHelper.SendFaultAsync(_binder, _replyFaultContext, _terminatingFault);
                    return;
                }
            }

            // Got here so the session did not actually send a fault, must clean up resources.
            if (_terminatingFault != null)
                _terminatingFault.Close();
            if (_replyFaultContext != null)
                _replyFaultContext.Abort();
            _binder.Abort();
        }

        private void OnInactivityElapsed(object state)
        {
            WsrmFault fault;
            Exception e;
            string exceptionMessage = SR.Format(SR.SequenceTerminatedInactivityTimeoutExceeded, _settings.InactivityTimeout);

            //if (TD.InactivityTimeoutIsEnabled())
            //{
            //    TD.InactivityTimeout(exceptionMessage);
            //}

            if (SequenceID != null)
            {
                string faultReason = SR.Format(SR.SequenceTerminatedInactivityTimeoutExceeded, _settings.InactivityTimeout);
                fault = SequenceTerminatedFault.CreateCommunicationFault(SequenceID, faultReason, exceptionMessage);
                e = fault.CreateException();
            }
            else
            {
                fault = null;
                e = new CommunicationException(exceptionMessage);
            }

            OnLocalFault(e, fault, null);
        }

        public abstract void OnLocalActivity();

        public void OnUnknownException(Exception e)
        {
            _canSendFault = false;
            OnLocalFault(e, (Message)null, null);
        }

        public virtual void OnRemoteActivity(bool fastPolling)
        {
            _inactivityTimer.Set();
        }

        // returns true if the info does not fault the session.
        public Task<bool> ProcessInfoAsync(WsrmMessageInfo info, RequestContext context)
        {
            return ProcessInfoAsync(info, context, false);
        }

        public async Task<bool> ProcessInfoAsync(WsrmMessageInfo info, RequestContext context, bool throwException)
        {
            Exception e;
            if (info.ParsingException != null)
            {
                WsrmFault fault;

                if (SequenceID != null)
                {
                    string reason = SR.Format(SR.CouldNotParseWithAction, info.Action);
                    fault = SequenceTerminatedFault.CreateProtocolFault(SequenceID, reason, null);
                }
                else
                {
                    fault = null;
                }

                e = new ProtocolException(SR.MessageExceptionOccurred, info.ParsingException);
                OnLocalFault(throwException ? null : e, fault, context);
            }
            else if (info.FaultReply != null)
            {
                e = info.FaultException;
                OnLocalFault(throwException ? null : e, info.FaultReply, context);
            }
            else if ((info.WsrmHeaderFault != null) && (info.WsrmHeaderFault.SequenceID != InputID)
                && (info.WsrmHeaderFault.SequenceID != OutputID))
            {
                e = new ProtocolException(SR.Format(SR.WrongIdentifierFault, FaultException.GetSafeReasonText(info.WsrmHeaderFault.Reason)));
                OnLocalFault(throwException ? null : e, (Message)null, context);
            }
            else if (info.FaultInfo != null)
            {
                if (_isSessionClosed)
                {
                    UnknownSequenceFault unknownSequenceFault = info.FaultInfo as UnknownSequenceFault;

                    if (unknownSequenceFault != null)
                    {
                        UniqueId sequenceId = unknownSequenceFault.SequenceID;

                        if (((OutputID != null) && (OutputID == sequenceId))
                            || ((InputID != null) && (InputID == sequenceId)))
                        {
                            if (_settings.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
                            {
                                info.Message.Close();
                                return false;
                            }
                            else if (_settings.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessaging11)
                            {
                                return true;
                            }
                            else
                            {
                                throw Fx.AssertAndThrow("Unknown version.");
                            }
                        }
                    }
                }

                e = info.FaultException;
                if (context != null)
                    await context.CloseAsync();
                OnRemoteFault(throwException ? null : e);
            }
            else
            {
                return true;
            }

            info.Message.Close();
            if (throwException)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(e);
            else
                return false;
        }

        public void SetFinalAck(SequenceRangeCollection finalRanges)
        {
            _finalRanges = finalRanges;
        }

        public virtual void StartInactivityTimer()
        {
            _inactivityTimer.Set();
        }

        // RM channels fault out of band. During the Closing and Closed states CommunicationObjects
        // do not fault. In all other states the RM channel can and must unblock various methods
        // from the OnFaulted method. This method will ensure that anything that needs to unblock
        // in the Closing state will unblock if a fault occurs.
        private void UnblockChannelIfNecessary()
        {
            lock (ThisLock)
            {
                if (_faulted == SessionFaultState.NotFaulted)
                {
                    throw Fx.AssertAndThrow("This method must be called from a fault thread.");
                }
                // Successfully faulted or aborted.
                else if (_faulted == SessionFaultState.CleanedUp)
                {
                    return;
                }
            }

            // Make sure the fault is sent then unblock the channel.
            OnFaulted();
            _unblockChannelCloseCallback();
        }

        public bool VerifyDuplexProtocolElements(WsrmMessageInfo info, RequestContext context)
        {
            return VerifyDuplexProtocolElements(info, context, false);
        }

        public bool VerifyDuplexProtocolElements(WsrmMessageInfo info, RequestContext context, bool throwException)
        {
            WsrmFault fault = VerifyDuplexProtocolElements(info);

            if (fault == null)
            {
                return true;
            }

            if (throwException)
            {
                Exception e = fault.CreateException();
                OnLocalFault(null, fault, context);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(e);
            }
            else
            {
                OnLocalFault(fault.CreateException(), fault, context);
                return false;
            }
        }

        protected virtual WsrmFault VerifyDuplexProtocolElements(WsrmMessageInfo info)
        {
            if (info.AcknowledgementInfo != null && info.AcknowledgementInfo.SequenceID != OutputID)
                return new UnknownSequenceFault(info.AcknowledgementInfo.SequenceID);
            else if (info.AckRequestedInfo != null && info.AckRequestedInfo.SequenceID != InputID)
                return new UnknownSequenceFault(info.AckRequestedInfo.SequenceID);
            else if (info.SequencedMessageInfo != null && info.SequencedMessageInfo.SequenceID != InputID)
                return new UnknownSequenceFault(info.SequencedMessageInfo.SequenceID);
            else if (info.TerminateSequenceInfo != null && info.TerminateSequenceInfo.Identifier != InputID)
            {
                if (Settings.ReliableMessagingVersion == ReliableMessagingVersion.WSReliableMessagingFebruary2005)
                    return SequenceTerminatedFault.CreateProtocolFault(OutputID, SR.SequenceTerminatedUnexpectedTerminateSequence, SR.UnexpectedTerminateSequence);
                else if (info.TerminateSequenceInfo.Identifier == OutputID)
                    return null;
                else
                    return new UnknownSequenceFault(info.TerminateSequenceInfo.Identifier);
            }
            else if (info.TerminateSequenceResponseInfo != null)
            {
                WsrmUtilities.AssertWsrm11(_settings.ReliableMessagingVersion);

                if (info.TerminateSequenceResponseInfo.Identifier == OutputID)
                    return null;
                else
                    return new UnknownSequenceFault(info.TerminateSequenceResponseInfo.Identifier);
            }
            else if (info.CloseSequenceInfo != null)
            {
                WsrmUtilities.AssertWsrm11(_settings.ReliableMessagingVersion);

                if (info.CloseSequenceInfo.Identifier == InputID)
                    return null;
                else if (info.CloseSequenceInfo.Identifier == OutputID)
                    // Spec allows RM-Destination close, but we do not.
                    return SequenceTerminatedFault.CreateProtocolFault(OutputID, SR.SequenceTerminatedUnsupportedClose, SR.UnsupportedCloseExceptionString);
                else
                    return new UnknownSequenceFault(info.CloseSequenceInfo.Identifier);
            }
            else if (info.CloseSequenceResponseInfo != null)
            {
                WsrmUtilities.AssertWsrm11(_settings.ReliableMessagingVersion);

                if (info.CloseSequenceResponseInfo.Identifier == OutputID)
                    return null;
                else if (info.CloseSequenceResponseInfo.Identifier == InputID)
                    return SequenceTerminatedFault.CreateProtocolFault(InputID, SR.SequenceTerminatedUnexpectedCloseSequenceResponse, SR.UnexpectedCloseSequenceResponse);
                else
                    return new UnknownSequenceFault(info.CloseSequenceResponseInfo.Identifier);
            }
            else
                return null;
        }

        public bool VerifySimplexProtocolElements(WsrmMessageInfo info, RequestContext context)
        {
            return VerifySimplexProtocolElements(info, context, false);
        }

        public bool VerifySimplexProtocolElements(WsrmMessageInfo info, RequestContext context, bool throwException)
        {
            WsrmFault fault = VerifySimplexProtocolElements(info);

            if (fault == null)
            {
                return true;
            }

            info.Message.Close();

            if (throwException)
            {
                Exception e = fault.CreateException();
                OnLocalFault(null, fault, context);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(e);
            }
            else
            {
                OnLocalFault(fault.CreateException(), fault, context);
                return false;
            }
        }

        protected abstract WsrmFault VerifySimplexProtocolElements(WsrmMessageInfo info);

        private enum SessionFaultState
        {
            NotFaulted,
            LocallyFaulted,
            RemotelyFaulted,
            CleanedUp
        }

        public delegate void UnblockChannelCloseHandler();
    }

    internal class ServerReliableSession : ChannelReliableSession, IInputSession
    {
        public ServerReliableSession(
            ServiceChannelBase channel,
            IReliableFactorySettings listener,
            IServerReliableChannelBinder binder,
            FaultHelper faultHelper,
            UniqueId inputID,
            UniqueId outputID) 
            : base(channel, listener, binder, faultHelper)
        {
            InputID = inputID;
            OutputID = outputID;
        }

        public override UniqueId SequenceID => InputID;

        public override void OnLocalActivity()
        {
        }

        public override Task OpenAsync(TimeSpan timeout)
        {
            StartInactivityTimer();
            return Task.CompletedTask;
        }

        protected override WsrmFault VerifyDuplexProtocolElements(WsrmMessageInfo info)
        {
            WsrmFault fault = base.VerifyDuplexProtocolElements(info);

            if (fault != null)
                return fault;
            else if (info.CreateSequenceInfo != null && info.CreateSequenceInfo.OfferIdentifier != OutputID)
                return SequenceTerminatedFault.CreateProtocolFault(OutputID, SR.SequenceTerminatedUnexpectedCSOfferId, SR.UnexpectedCSOfferId);
            else if (info.CreateSequenceResponseInfo != null)
                return SequenceTerminatedFault.CreateProtocolFault(OutputID, SR.SequenceTerminatedUnexpectedCSR, SR.UnexpectedCSR);
            else
                return null;
        }

        protected override WsrmFault VerifySimplexProtocolElements(WsrmMessageInfo info)
        {
            if (info.AcknowledgementInfo != null)
                return SequenceTerminatedFault.CreateProtocolFault(InputID, SR.SequenceTerminatedUnexpectedAcknowledgement, SR.UnexpectedAcknowledgement);
            else if (info.AckRequestedInfo != null && info.AckRequestedInfo.SequenceID != InputID)
                return new UnknownSequenceFault(info.AckRequestedInfo.SequenceID);
            else if (info.CreateSequenceResponseInfo != null)
                return SequenceTerminatedFault.CreateProtocolFault(InputID, SR.SequenceTerminatedUnexpectedCSR, SR.UnexpectedCSR);
            else if (info.SequencedMessageInfo != null && info.SequencedMessageInfo.SequenceID != InputID)
                return new UnknownSequenceFault(info.SequencedMessageInfo.SequenceID);
            else if (info.TerminateSequenceInfo != null && info.TerminateSequenceInfo.Identifier != InputID)
                return new UnknownSequenceFault(info.TerminateSequenceInfo.Identifier);
            else if (info.TerminateSequenceResponseInfo != null)
            {
                WsrmUtilities.AssertWsrm11(Settings.ReliableMessagingVersion);

                if (info.TerminateSequenceResponseInfo.Identifier == InputID)
                    return SequenceTerminatedFault.CreateProtocolFault(InputID, SR.SequenceTerminatedUnexpectedTerminateSequenceResponse, SR.UnexpectedTerminateSequenceResponse);
                else
                    return new UnknownSequenceFault(info.TerminateSequenceResponseInfo.Identifier);
            }
            else if (info.CloseSequenceInfo != null)
            {
                WsrmUtilities.AssertWsrm11(Settings.ReliableMessagingVersion);

                if (info.CloseSequenceInfo.Identifier == InputID)
                    return null;
                else
                    return new UnknownSequenceFault(info.CloseSequenceInfo.Identifier);
            }
            else if (info.CloseSequenceResponseInfo != null)
            {
                WsrmUtilities.AssertWsrm11(Settings.ReliableMessagingVersion);

                if (info.CloseSequenceResponseInfo.Identifier == InputID)
                    return SequenceTerminatedFault.CreateProtocolFault(InputID, SR.SequenceTerminatedUnexpectedCloseSequenceResponse, SR.UnexpectedCloseSequenceResponse);
                else
                    return new UnknownSequenceFault(info.CloseSequenceResponseInfo.Identifier);
            }
            else
                return null;
        }
    }
}
