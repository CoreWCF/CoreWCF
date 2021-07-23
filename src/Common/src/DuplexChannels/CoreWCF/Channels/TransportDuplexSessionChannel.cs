// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Security.Authentication.ExtendedProtection;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    internal abstract class TransportDuplexSessionChannel : TransportOutputChannel, IDuplexSessionChannel
    {
        private bool _isInputSessionClosed;
        private bool _isOutputSessionClosed;
        private ChannelBinding _channelBindingToken;
        private TaskCompletionSource<object> _inputSessionClosedTcs;

        protected TransportDuplexSessionChannel(
          ITransportFactorySettings settings,
          EndpointAddress localAddress,
          Uri localVia,
          EndpointAddress remoteAddress,
          Uri via)
        : base(settings, remoteAddress, via, settings.ManualAddressing, settings.MessageVersion)
        {
            LocalAddress = localAddress;
            LocalVia = localVia;
            BufferManager = settings.BufferManager;
            MessageEncoder = settings.MessageEncoderFactory.CreateSessionEncoder();
            Session = new ConnectionDuplexSession(this);
            _inputSessionClosedTcs = new TaskCompletionSource<object>();
        }

        public EndpointAddress LocalAddress { get; }

        public SecurityMessageProperty RemoteSecurity { get; protected set; }

        public IDuplexSession Session { get; protected set; }

        public SemaphoreSlim SendLock { get; } = new SemaphoreSlim(1);

        protected ChannelBinding ChannelBinding
        {
            get
            {
                return _channelBindingToken;
            }
        }

        protected BufferManager BufferManager { get; }

        protected Uri LocalVia { get; }

        protected MessageEncoder MessageEncoder { get; set; }

        protected SynchronizedMessageSource MessageSource { get; private set; }

        protected abstract bool IsStreamedOutput { get; }

        public async Task StartReceivingAsync()
        {
            if (ChannelDispatcher == null)
            {
                // TODO: Cleanup exception message, find a SR to use and add Fx error handling
                throw new InvalidOperationException("ChannelDispatcher isn't set");
            }

            while (true)
            {
                (Message message, bool success) = await TryReceiveAsync(CancellationToken.None);
                if (success)
                {
                    await ChannelDispatcher.DispatchAsync(message);
                }

                if (message == null || this.DoneReceivingInCurrentState()) // NULL message means client sent FIN byte
                {
                    return;
                }
            }
        }

        public async Task<Message> ReceiveAsync(CancellationToken token)
        {
            Message message = null;
            if (this.DoneReceivingInCurrentState())
            {
                return null;
            }

            bool shouldFault = true;
            try
            {
                message = await MessageSource.ReceiveAsync(token);
                OnReceiveMessage(message);
                shouldFault = false;
                return message;
            }
            finally
            {
                if (shouldFault)
                {
                    if (message != null)
                    {
                        message.Close();
                    }

                    Fault();
                }
            }
        }

        public async Task<(Message message, bool success)> TryReceiveAsync(CancellationToken token)
        {
            try
            {
                return (await ReceiveAsync(token), true);
            }
            catch (TimeoutException e)
            {
                //if (TD.ReceiveTimeoutIsEnabled())
                //{
                //    TD.ReceiveTimeout(e.Message);
                //}

                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);

                return (null, false);
            }
        }

        protected void SetChannelBinding(ChannelBinding channelBinding)
        {
            Fx.Assert(_channelBindingToken == null, "ChannelBinding token can only be set once.");
            _channelBindingToken = channelBinding;
        }

        protected async Task CloseOutputSessionAsync(CancellationToken token)
        {
            ThrowIfNotOpened();
            ThrowIfFaulted();
            try
            {
                await SendLock.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                // TODO: Fix the timeout value reported
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(
                                                SR.Format(SR.CloseTimedOut, TimeSpan.Zero),
                                                TimeoutHelper.CreateEnterTimedOutException(TimeSpan.Zero)));
            }

            try
            {
                // check again in case the previous send faulted while we were waiting for the lock
                ThrowIfFaulted();

                // we're synchronized by sendLock here
                if (_isOutputSessionClosed)
                {
                    return;
                }

                _isOutputSessionClosed = true;
                bool shouldFault = true;
                try
                {
                    await CloseOutputSessionCoreAsync(token);
                    OnOutputSessionClosed(token);
                    shouldFault = false;
                }
                finally
                {
                    if (shouldFault)
                    {
                        Fault();
                    }
                }
            }
            finally
            {
                SendLock.Release();
            }
        }

        protected void SetMessageSource(IMessageSource messageSource)
        {
            MessageSource = new SynchronizedMessageSource(messageSource);
        }

        protected abstract Task CloseOutputSessionCoreAsync(CancellationToken token);

        // used to return cached connection to the pool/reader pool
        protected abstract void ReturnConnectionIfNecessary(bool abort, CancellationToken token);

        protected override void OnAbort()
        {
            ReturnConnectionIfNecessary(true, CancellationToken.None);
        }

        protected override void OnFaulted()
        {
            base.OnFaulted();
            ReturnConnectionIfNecessary(true, CancellationToken.None);
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            await CloseOutputSessionAsync(token);

            // close input session if necessary
            if (!_isInputSessionClosed)
            {
                await EnsureInputClosedAsync(token);
                OnInputSessionClosed();
            }

            await CompleteCloseAsync(token);
        }

        protected override void OnClosed()
        {
            base.OnClosed();

            // clean up the CBT after transitioning to the closed state
            ChannelBindingUtility.Dispose(ref _channelBindingToken);
        }

        protected virtual void OnReceiveMessage(Message message)
        {
            if (message == null)
            {
                OnInputSessionClosed();
            }
            else
            {
                PrepareMessage(message);
            }
        }

        protected void ApplyChannelBinding(Message message)
        {
            ChannelBindingUtility.TryAddToMessage(_channelBindingToken, message, false);
        }

        protected virtual void PrepareMessage(Message message)
        {
            message.Properties.Via = LocalVia;

            ApplyChannelBinding(message);

            //if (FxTrace.Trace.IsEnd2EndActivityTracingEnabled)
            //{
            //    EventTraceActivity eventTraceActivity = EventTraceActivityHelper.TryExtractActivity(message);
            //    Guid relatedActivityId = EventTraceActivity.GetActivityIdFromThread();
            //    if (eventTraceActivity == null)
            //    {
            //        eventTraceActivity = EventTraceActivity.GetFromThreadOrCreate();
            //        EventTraceActivityHelper.TryAttachActivity(message, eventTraceActivity);
            //    }

            //    if (TD.MessageReceivedByTransportIsEnabled())
            //    {
            //        TD.MessageReceivedByTransport(
            //            eventTraceActivity,
            //            this.LocalAddress != null && this.LocalAddress.Uri != null ? this.LocalAddress.Uri.AbsoluteUri : string.Empty,
            //            relatedActivityId);
            //    }
            //}

            //if (DiagnosticUtility.ShouldTraceInformation)
            //{
            //    TraceUtility.TraceEvent(
            //                 TraceEventType.Information,
            //                 TraceCode.MessageReceived,
            //                 SR.GetString(SR.TraceCodeMessageReceived),
            //                 MessageTransmitTraceRecord.CreateReceiveTraceRecord(message, this.LocalAddress),
            //                 this,
            //                 null,
            //                 message);
            //}
        }

        protected abstract Task CloseOutputAsync(CancellationToken token);

        protected abstract ArraySegment<byte> EncodeMessage(Message message);

        protected abstract Task OnSendCoreAsync(Message message, CancellationToken token);

        protected override async Task OnSendAsync(Message message, CancellationToken token)
        {
            ThrowIfDisposedOrNotOpen();

            try
            {
                await SendLock.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                // TODO: Fix the timeout value reported
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(
                                                SR.Format(SR.SendToViaTimedOut, TimeSpan.Zero),
                                                TimeoutHelper.CreateEnterTimedOutException(TimeSpan.Zero)));
            }

            try
            {
                // check again in case the previous send faulted while we were waiting for the lock
                ThrowIfDisposedOrNotOpen();
                ThrowIfOutputSessionClosed();

                bool success = false;
                try
                {
                    ApplyChannelBinding(message);

                    await OnSendCoreAsync(message, token);
                    success = true;
                }
                finally
                {
                    if (!success)
                    {
                        Fault();
                    }
                }
            }
            finally
            {
                SendLock.Release();
            }
        }

        // cleanup after the framing handshake has completed
        protected abstract Task CompleteCloseAsync(CancellationToken token);

        private void ThrowIfOutputSessionClosed()
        {
            if (_isOutputSessionClosed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SendCannotBeCalledAfterCloseOutputSession));
            }
        }

        private async Task EnsureInputClosedAsync(CancellationToken token)
        {
            Message message = await MessageSource.ReceiveAsync(token);
            if (message != null)
            {
                using (message)
                {
                    ProtocolException error = ReceiveShutdownReturnedNonNull(message);
                    throw TraceUtility.ThrowHelperError(error, message);
                }
            }
        }

        private void OnInputSessionClosed()
        {
            lock (ThisLock)
            {
                if (_isInputSessionClosed)
                {
                    return;
                }

                _isInputSessionClosed = true;
                _inputSessionClosedTcs.TrySetResult(null);
            }
        }

        private void OnOutputSessionClosed(CancellationToken token)
        {
            bool releaseConnection = false;
            lock (ThisLock)
            {
                if (_isInputSessionClosed)
                {
                    // we're all done, release the connection
                    releaseConnection = true;
                }
            }

            if (releaseConnection)
            {
                ReturnConnectionIfNecessary(false, token);
            }
        }

        internal void ThrowIfFaulted()
        {
            ThrowPending();

            switch (State)
            {
                case CommunicationState.Created:
                    break;

                case CommunicationState.Opening:
                    break;

                case CommunicationState.Opened:
                    break;

                case CommunicationState.Closing:
                    break;

                case CommunicationState.Closed:
                    break;

                case CommunicationState.Faulted:
                    throw TraceUtility.ThrowHelperError(CreateFaultedException(), Guid.Empty, this);

                default:
                    throw Fx.AssertAndThrow("ThrowIfFaulted: Unknown CommunicationObject.state");
            }
        }

        internal Exception CreateFaultedException()
        {
            string message = SR.Format(SR.CommunicationObjectFaulted1, GetCommunicationObjectType().ToString());
            return new CommunicationObjectFaultedException(message);
        }

        internal ProtocolException ReceiveShutdownReturnedNonNull(Message message)
        {
            if (message.IsFault)
            {
                try
                {
                    MessageFault fault = MessageFault.CreateFault(message, 64 * 1024);
                    FaultReasonText reason = fault.Reason.GetMatchingTranslation(CultureInfo.CurrentCulture);
                    string text = SR.Format(SR.ReceiveShutdownReturnedFault, reason.Text);
                    return new ProtocolException(text);
                }
                catch (QuotaExceededException)
                {
                    string text = SR.Format(SR.ReceiveShutdownReturnedLargeFault, message.Headers.Action);
                    return new ProtocolException(text);
                }
            }
            else
            {
                string text = SR.Format(SR.ReceiveShutdownReturnedMessage, message.Headers.Action);
                return new ProtocolException(text);
            }
        }

        internal class ConnectionDuplexSession : IDuplexSession
        {
            private static UriGenerator s_uriGenerator;
            private string _id;

            public ConnectionDuplexSession(TransportDuplexSessionChannel channel)
                : base()
            {
                Channel = channel;
            }

            public string Id
            {
                get
                {
                    if (_id == null)
                    {
                        lock (Channel)
                        {
                            if (_id == null)
                            {
                                _id = UriGenerator.Next();
                            }
                        }
                    }

                    return _id;
                }
            }

            public TransportDuplexSessionChannel Channel { get; }

            private static UriGenerator UriGenerator
            {
                get
                {
                    if (s_uriGenerator == null)
                    {
                        s_uriGenerator = new UriGenerator();
                    }

                    return s_uriGenerator;
                }
            }

            public Task CloseOutputSessionAsync()
            {
                var timeoutHelper = new TimeoutHelper(Channel.DefaultCloseTimeout);
                return CloseOutputSessionAsync(timeoutHelper.GetCancellationToken());
            }

            public Task CloseOutputSessionAsync(CancellationToken token)
            {
                return Channel.CloseOutputSessionAsync(token);
            }
        }
    }
}
