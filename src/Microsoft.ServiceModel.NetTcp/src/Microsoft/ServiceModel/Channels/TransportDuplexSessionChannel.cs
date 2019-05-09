using System;
using System.Diagnostics;
using Microsoft.Runtime;
using Microsoft.Runtime.Diagnostics;
using System.Security.Authentication.ExtendedProtection;
using Microsoft.ServiceModel;
using Microsoft.ServiceModel.Diagnostics;
using Microsoft.ServiceModel.Security;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    abstract class TransportDuplexSessionChannel : TransportOutputChannel, IDuplexSessionChannel
    {
        BufferManager bufferManager;
        IDuplexSession duplexSession;
        bool isInputSessionClosed;
        bool isOutputSessionClosed;
        MessageEncoder messageEncoder;
        SynchronizedMessageSource messageSource;
        SecurityMessageProperty remoteSecurity;
        EndpointAddress localAddress;
        SemaphoreSlim sendLock;
        Uri localVia;
        ChannelBinding channelBindingToken;

        protected TransportDuplexSessionChannel(
                  ChannelManagerBase manager,
                  ITransportFactorySettings settings,
                  EndpointAddress localAddress,
                  Uri localVia,
                  EndpointAddress remoteAddresss,
                  Uri via)
                : base(manager, remoteAddresss, via, settings.ManualAddressing, settings.MessageVersion)
        {
            this.localAddress = localAddress;
            this.localVia = localVia;
            bufferManager = settings.BufferManager;
            sendLock = new SemaphoreSlim(1);
            messageEncoder = settings.MessageEncoderFactory.CreateSessionEncoder();
            Session = new ConnectionDuplexSession(this);
        }

        public EndpointAddress LocalAddress
        {
            get { return localAddress; }
        }

        public SecurityMessageProperty RemoteSecurity
        {
            get { return remoteSecurity; }
            protected set { remoteSecurity = value; }
        }

        public IDuplexSession Session
        {
            get { return duplexSession; }
            protected set { duplexSession = value; }
        }

        public SemaphoreSlim SendLock
        {
            get { return sendLock; }
        }

        protected ChannelBinding ChannelBinding
        {
            get
            {
                return channelBindingToken;
            }
        }

        protected BufferManager BufferManager
        {
            get
            {
                return bufferManager;
            }
        }

        protected Uri LocalVia
        {
            get { return localVia; }
        }

        protected MessageEncoder MessageEncoder
        {
            get { return messageEncoder; }
            set { messageEncoder = value; }
        }

        protected SynchronizedMessageSource MessageSource
        {
            get { return messageSource; }
        }

        protected abstract bool IsStreamedOutput { get; }

        public Task<Message> ReceiveAsync()
        {
            var timeoutHelper = new TimeoutHelper(DefaultReceiveTimeout);
            return ReceiveAsync(timeoutHelper.GetCancellationToken());
        }

        public async Task<Message> ReceiveAsync(CancellationToken token)
        {
            Message message = null;
            if (DoneReceivingInCurrentState())
            {
                return null;
            }

            bool shouldFault = true;
            try
            {
                message = await messageSource.ReceiveAsync(token);
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
                        message = null;
                    }

                    Fault();
                }
            }
        }

        // TODO: Move these methods which are copied from CommunicationObject to common helper class
        internal bool DoneReceivingInCurrentState()
        {
            ThrowPending();

            switch (State)
            {
                case CommunicationState.Created:
                    throw TraceUtility.ThrowHelperError(CreateNotOpenException(), Guid.Empty, this);

                case CommunicationState.Opening:
                    throw TraceUtility.ThrowHelperError(CreateNotOpenException(), Guid.Empty, this);

                case CommunicationState.Opened:
                    return false;

                case CommunicationState.Closing:
                    return true;

                case CommunicationState.Closed:
                    return true;

                case CommunicationState.Faulted:
                    return true;

                default:
                    throw Fx.AssertAndThrow("DoneReceivingInCurrentState: Unknown CommunicationObject.state");
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

        private Exception CreateNotOpenException()
        {
            return new InvalidOperationException(SR.Format(SR.CommunicationObjectCannotBeUsed, GetCommunicationObjectType().ToString(), State.ToString()));
        }

        public async Task<TryAsyncResult<Message>> TryReceiveAsync(CancellationToken token)
        {
            try
            {
                var message = await ReceiveAsync(token);
                return TryAsyncResult.FromResult(message);
            }
            catch (TimeoutException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                return TryAsyncResult<Message>.FailedResult;
            }
        }

        public async Task<bool> WaitForMessageAsync(CancellationToken token)
        {
            if (DoneReceivingInCurrentState())
            {
                return true;
            }

            bool shouldFault = true;
            try
            {
                bool success = await messageSource.WaitForMessageAsync(token);
                shouldFault = !success; // need to fault if we've timed out because we're now toast
                return success;
            }
            finally
            {
                if (shouldFault)
                {
                    Fault();
                }
            }
        }

        protected void SetChannelBinding(ChannelBinding channelBinding)
        {
            Fx.Assert(channelBindingToken == null, "ChannelBinding token can only be set once.");
            channelBindingToken = channelBinding;
        }

        protected void SetMessageSource(IMessageSource messageSource)
        {
            this.messageSource = new SynchronizedMessageSource(messageSource);
        }

        protected async Task CloseOutputSessionAsync(CancellationToken token)
        {
            ThrowIfNotOpened();
            ThrowIfFaulted();

            try
            {
                await sendLock.WaitAsync(token);
            }
            catch(OperationCanceledException)
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
                if (isOutputSessionClosed)
                {
                    return;
                }

                isOutputSessionClosed = true;
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
                sendLock.Release();
            }

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
            if (!isInputSessionClosed)
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
            ChannelBindingUtility.Dispose(ref channelBindingToken);
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
            ChannelBindingUtility.TryAddToMessage(channelBindingToken, message, false);
        }

        protected virtual void PrepareMessage(Message message)
        {
            message.Properties.Via = localVia;

            ApplyChannelBinding(message);
        }

        protected abstract Task StartWritingBufferedMessageAsync(Message message, ArraySegment<byte> messageData, bool allowOutputBatching, CancellationToken token);

        protected abstract Task CloseOutputAsync(CancellationToken token);

        protected virtual void FinishWritingMessage()
        {
        }

        protected abstract ArraySegment<byte> EncodeMessage(Message message);

        protected abstract void OnSendCore(Message message, TimeSpan timeout);

        protected abstract Task StartWritingStreamedMessageAsync(Message message, CancellationToken token);

        protected override async Task OnSendAsync(Message message, CancellationToken token)
        {
            ThrowIfDisposedOrNotOpen();

            try
            {
                await sendLock.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                // TODO: Fix the timeout value reported
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(
                                                SR.Format(SR.SendToViaTimedOut, TimeSpan.Zero),
                                                TimeoutHelper.CreateEnterTimedOutException(TimeSpan.Zero)));
            }

            byte[] buffer = null;

            try
            {
                // check again in case the previous send faulted while we were waiting for the lock
                ThrowIfDisposedOrNotOpen();
                ThrowIfOutputSessionClosed();

                bool success = false;
                try
                {
                    ApplyChannelBinding(message);

                    if (IsStreamedOutput)
                    {
                        await StartWritingStreamedMessageAsync(message, token);
                    }
                    else
                    {
                        bool allowOutputBatching;
                        ArraySegment<byte> messageData;
                        allowOutputBatching = message.Properties.AllowOutputBatching;
                        messageData = EncodeMessage(message);

                        buffer = messageData.Array;
                        await StartWritingBufferedMessageAsync(
                                    message,
                                    messageData,
                                    allowOutputBatching,
                                    token);
                    }

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
                sendLock.Release();
            }
            if (buffer != null)
            {
                bufferManager.ReturnBuffer(buffer);
            }
        }

        // cleanup after the framing handshake has completed
        protected abstract Task CompleteCloseAsync(CancellationToken token);

        // must be called under sendLock 
        void ThrowIfOutputSessionClosed()
        {
            if (isOutputSessionClosed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SendCannotBeCalledAfterCloseOutputSession)));
            }
        }

        async Task EnsureInputClosedAsync(CancellationToken token)
        {
            Message message = await MessageSource.ReceiveAsync(token);
            if (message != null)
            {
                using (message)
                {
                    ProtocolException error = ProtocolExceptionHelper.ReceiveShutdownReturnedNonNull(message);
                    throw TraceUtility.ThrowHelperError(error, message);
                }
            }
        }

        void OnInputSessionClosed()
        {
            lock (ThisLock)
            {
                if (isInputSessionClosed)
                {
                    return;
                }

                isInputSessionClosed = true;
            }
        }

        void OnOutputSessionClosed(CancellationToken token)
        {
            bool releaseConnection = false;
            lock (ThisLock)
            {
                if (isInputSessionClosed)
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

        internal class ConnectionDuplexSession : IDuplexSession
        {
            static UriGenerator uriGenerator;
            TransportDuplexSessionChannel channel;
            string id;

            public ConnectionDuplexSession(TransportDuplexSessionChannel channel)
                : base()
            {
                this.channel = channel;
            }

            public string Id
            {
                get
                {
                    if (id == null)
                    {
                        lock (channel)
                        {
                            if (id == null)
                            {
                                id = UriGenerator.Next();
                            }
                        }
                    }

                    return id;
                }
            }

            public TransportDuplexSessionChannel Channel
            {
                get { return channel; }
            }

            static UriGenerator UriGenerator
            {
                get
                {
                    if (uriGenerator == null)
                    {
                        uriGenerator = new UriGenerator();
                    }

                    return uriGenerator;
                }
            }

            public Task CloseOutputSessionAsync()
            {
                var timeoutHelper = new TimeoutHelper(channel.DefaultCloseTimeout);
                return CloseOutputSessionAsync(timeoutHelper.GetCancellationToken());
            }

            public Task CloseOutputSessionAsync(CancellationToken token)
            {
                return channel.CloseOutputSessionAsync(token);
            }
        }
    }
}
