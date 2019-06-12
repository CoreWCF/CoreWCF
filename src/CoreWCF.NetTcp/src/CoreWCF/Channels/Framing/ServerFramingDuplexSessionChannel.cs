using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Runtime;
using CoreWCF.Security;
using System;
using System.Security.Authentication.ExtendedProtection;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace CoreWCF.Channels.Framing
{
    internal class ServerFramingDuplexSessionChannel : FramingDuplexSessionChannel
    {
        StreamUpgradeAcceptor upgradeAcceptor;
        private IServiceProvider _serviceProvider;
        IStreamUpgradeChannelBindingProvider channelBindingProvider;

        public ServerFramingDuplexSessionChannel(FramingConnection connection, ITransportFactorySettings settings,
            bool exposeConnectionProperty, IServiceProvider serviceProvider)
            : base(connection, settings, exposeConnectionProperty)
        {
            Connection = connection;
            upgradeAcceptor = connection.StreamUpgradeAcceptor;
            _serviceProvider = serviceProvider;
            //if (upgradeAcceptor != null)
            //{
            //    this.channelBindingProvider = upgrade.GetProperty<IStreamUpgradeChannelBindingProvider>();
            //    this.upgradeAcceptor = upgrade.CreateUpgradeAcceptor();
            //}
        }

        protected override void ReturnConnectionIfNecessary(bool abort, CancellationToken token)
        {
            // TODO: Put connection back into the beginning of the middleware stack
            //    IConnection localConnection = null;
            //    if (this.sessionReader != null)
            //    {
            //        lock (ThisLock)
            //        {
            //            localConnection = this.sessionReader.GetRawConnection();
            //        }
            //    }

            //    if (localConnection != null)
            //    {
            //        if (abort)
            //        {
            //            localConnection.Abort();
            //        }
            //        else
            //        {
            //            this.connectionDemuxer.ReuseConnection(localConnection, timeout);
            //        }
            //        this.connectionDemuxer = null;
            //    }
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IChannelBindingProvider))
            {
                return (T)(object)channelBindingProvider;
            }

            T service = _serviceProvider.GetService<T>();
            if (service != null)
            {
                return service;
            }

            return base.GetProperty<T>();
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask; // NOOP
        }

    }

    internal abstract class FramingDuplexSessionChannel : TransportDuplexSessionChannel
    {
        bool exposeConnectionProperty;

        private FramingDuplexSessionChannel(ITransportFactorySettings settings,
            EndpointAddress localAddress, Uri localVia, EndpointAddress remoteAddresss, Uri via, bool exposeConnectionProperty)
            : base(settings, localAddress, localVia, remoteAddresss, via)
        {
            this.exposeConnectionProperty = exposeConnectionProperty;
        }

        protected FramingDuplexSessionChannel(FramingConnection connection, ITransportFactorySettings settings, bool exposeConnectionProperty)
    : this(settings, new EndpointAddress(connection.ServiceDispatcher.BaseAddress), connection.Via,
    EndpointAddress.AnonymousAddress, connection.MessageEncoder.MessageVersion.Addressing.AnonymousUri, exposeConnectionProperty)
        {
            Session = FramingConnectionDuplexSession.CreateSession(this, connection.StreamUpgradeAcceptor);
        }

        protected FramingConnection Connection { get; set; }

        protected override bool IsStreamedOutput
        {
            get { return false; }
        }

        protected override async Task CloseOutputSessionCoreAsync(CancellationToken token)
        {
            var timeout = TimeoutHelper.GetOriginalTimeout(token);
            await Connection.Output.WriteAsync(SessionEncoder.EndBytes, token);
            await Connection.Output.FlushAsync();
        }

        protected override Task CompleteCloseAsync(CancellationToken token)
        {
            ReturnConnectionIfNecessary(false, token);
            return Task.CompletedTask;
        }

        protected override async Task OnSendCoreAsync(Message message, CancellationToken token)
        {
            bool allowOutputBatching;
            ArraySegment<byte> messageData;
            allowOutputBatching = message.Properties.AllowOutputBatching;
            messageData = EncodeMessage(message);
            await Connection.Output.WriteAsync(messageData, token);
            if (!allowOutputBatching)
            {
                await Connection.Output.FlushAsync();
            }
        }

        protected override async Task StartWritingBufferedMessageAsync(Message message, ArraySegment<byte> messageData, bool allowOutputBatching, CancellationToken token)
        {
            await Connection.Output.WriteAsync(messageData, token);
            if (!allowOutputBatching)
            {
                await Connection.Output.FlushAsync();
            }
        }

        protected override async Task CloseOutputAsync(CancellationToken token)
        {
            await Connection.Output.WriteAsync(SessionEncoder.EndBytes, token);
            await Connection.Output.FlushAsync();
        }

        protected override Task StartWritingStreamedMessageAsync(Message message, CancellationToken token)
        {
            Fx.Assert(false, "Streamed output should never be called in this channel.");
            return Task.FromException(Fx.Exception.AsError(new InvalidOperationException()));
        }

        protected override ArraySegment<byte> EncodeMessage(Message message)
        {
            ArraySegment<byte> messageData = MessageEncoder.WriteMessage(message,
                int.MaxValue, BufferManager, SessionEncoder.MaxMessageFrameSize);

            messageData = SessionEncoder.EncodeMessageFrame(messageData);

            return messageData;
        }

        class FramingConnectionDuplexSession : ConnectionDuplexSession
        {

            FramingConnectionDuplexSession(FramingDuplexSessionChannel channel)
                : base(channel)
            {
            }

            public static FramingConnectionDuplexSession CreateSession(FramingDuplexSessionChannel channel,
                StreamUpgradeAcceptor upgradeAcceptor)
            {
                StreamSecurityUpgradeAcceptor security = upgradeAcceptor as StreamSecurityUpgradeAcceptor;
                if (security == null)
                {
                    return new FramingConnectionDuplexSession(channel);
                }
                else
                {
                    return new SecureConnectionDuplexSession(channel);
                }
            }

            class SecureConnectionDuplexSession : FramingConnectionDuplexSession, ISecuritySession
            {
                EndpointIdentity remoteIdentity;

                public SecureConnectionDuplexSession(FramingDuplexSessionChannel channel)
                    : base(channel)
                {
                    // empty
                }

                EndpointIdentity ISecuritySession.RemoteIdentity
                {
                    get
                    {
                        if (remoteIdentity == null)
                        {
                            SecurityMessageProperty security = Channel.RemoteSecurity;
                            if (security != null && security.ServiceSecurityContext != null &&
                                security.ServiceSecurityContext.IdentityClaim != null &&
                                security.ServiceSecurityContext.PrimaryIdentity != null)
                            {
                                remoteIdentity = EndpointIdentity.CreateIdentity(
                                    security.ServiceSecurityContext.IdentityClaim);
                            }
                        }

                        return remoteIdentity;
                    }
                }
            }
        }
    }

    internal abstract class TransportDuplexSessionChannel : TransportOutputChannel, IDuplexSessionChannel
    {
        IDuplexSession _duplexSession;
        bool _isInputSessionClosed;
        bool _isOutputSessionClosed;
        EndpointAddress _localAddress;
        ChannelBinding _channelBindingToken;

        protected TransportDuplexSessionChannel(
          ITransportFactorySettings settings,
          EndpointAddress localAddress,
          Uri localVia,
          EndpointAddress remoteAddresss,
          Uri via)
        : base(settings, remoteAddresss, via, settings.ManualAddressing, settings.MessageVersion)
        {
            LocalAddress = localAddress;
            LocalVia = localVia;
            BufferManager = settings.BufferManager;
            MessageEncoder = settings.MessageEncoderFactory.CreateSessionEncoder();
            Session = new ConnectionDuplexSession(this);
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

        protected abstract bool IsStreamedOutput { get; }

        public Task<Message> ReceiveAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Message> ReceiveAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<TryAsyncResult<Message>> TryReceiveAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<bool> WaitForMessageAsync(CancellationToken token)
        {
            throw new NotImplementedException();
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
                // TODO: Come up with some way to know when the input is closed. Maybe register something on the connection transport or have a Task which gets completed on close
                //await EnsureInputClosedAsync(token);
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

        protected void ApplyChannelBinding(Message message)
        {
            ChannelBindingUtility.TryAddToMessage(_channelBindingToken, message, false);
        }

        protected abstract Task StartWritingBufferedMessageAsync(Message message, ArraySegment<byte> messageData, bool allowOutputBatching, CancellationToken token);

        protected abstract Task CloseOutputAsync(CancellationToken token);

        protected abstract ArraySegment<byte> EncodeMessage(Message message);

        protected abstract Task OnSendCoreAsync(Message message, CancellationToken token);

        protected abstract Task StartWritingStreamedMessageAsync(Message message, CancellationToken token);

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

        void ThrowIfOutputSessionClosed()
        {
            if (_isOutputSessionClosed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SendCannotBeCalledAfterCloseOutputSession));
            }
        }

        void OnInputSessionClosed()
        {
            lock (ThisLock)
            {
                if (_isInputSessionClosed)
                {
                    return;
                }

                _isInputSessionClosed = true;
            }
        }

        void OnOutputSessionClosed(CancellationToken token)
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

        internal class ConnectionDuplexSession : IDuplexSession
        {
            static UriGenerator _uriGenerator;
            string _id;

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

            static UriGenerator UriGenerator
            {
                get
                {
                    if (_uriGenerator == null)
                    {
                        _uriGenerator = new UriGenerator();
                    }

                    return _uriGenerator;
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

    internal abstract class TransportOutputChannel : OutputChannel
    {
        private bool _anyHeadersToAdd;
        private EndpointAddress _to;
        private Uri _via;
        private ToHeader _toHeader;

        protected TransportOutputChannel(IDefaultCommunicationTimeouts timeouts, EndpointAddress to, Uri via, bool manualAddressing, MessageVersion messageVersion)
            : base(timeouts)
        {
            ManualAddressing = manualAddressing;
            MessageVersion = messageVersion;
            _to = to;
            _via = via;

            if (!manualAddressing && _to != null)
            {
                Uri toUri;
                if (_to.IsAnonymous)
                {
                    toUri = MessageVersion.Addressing.AnonymousUri;
                }
                else if (_to.IsNone)
                {
                    toUri = MessageVersion.Addressing.NoneUri;
                }
                else
                {
                    toUri = _to.Uri;
                }

                if (toUri != null)
                {
                    XmlDictionaryString dictionaryTo = new ToDictionary(toUri.AbsoluteUri).To;
                    _toHeader = ToHeader.Create(toUri, dictionaryTo, messageVersion.Addressing);
                }

                _anyHeadersToAdd = _to.Headers.Count > 0;
            }
        }

        protected bool ManualAddressing { get; }

        public MessageVersion MessageVersion { get; }

        public override EndpointAddress RemoteAddress
        {
            get
            {
                return _to;
            }
        }

        public override Uri Via
        {
            get
            {
                return _via;
            }
        }

        protected override void AddHeadersTo(Message message)
        {
            base.AddHeadersTo(message);

            if (_toHeader != null)
            {
                // TODO: Removed performance enhancement to avoid exposing another internal method.
                // Evaluate whether we should do something to bring this back. My thoughts are we 
                // remove the SetToHeader method as we should be using the same mechanism as third
                // parties transports have to use.
                message.Headers.To = _toHeader.To;

                // Original comment and code:
                // we don't use to.ApplyTo(message) since it's faster to cache and
                // use the actual <To> header then to call message.Headers.To = Uri...
                //message.Headers.SetToHeader(toHeader);

                if (_anyHeadersToAdd)
                {
                    _to.Headers.AddHeadersTo(message);
                }
            }
        }

        private class ToDictionary : IXmlDictionary
        {
            private XmlDictionaryString to;

            public ToDictionary(string to)
            {
                this.to = new XmlDictionaryString(this, to, 0);
            }

            public XmlDictionaryString To
            {
                get
                {
                    return to;
                }
            }

            public bool TryLookup(string value, out XmlDictionaryString result)
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                if (value == to.Value)
                {
                    result = to;
                    return true;
                }
                result = null;
                return false;
            }

            public bool TryLookup(int key, out XmlDictionaryString result)
            {
                if (key == 0)
                {
                    result = to;
                    return true;
                }
                result = null;
                return false;
            }

            public bool TryLookup(XmlDictionaryString value, out XmlDictionaryString result)
            {
                if (value == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                if (value == to)
                {
                    result = to;
                    return true;
                }
                result = null;
                return false;
            }
        }
    }

    internal abstract class OutputChannel : ServiceChannelBase, IOutputChannel
    {
        protected OutputChannel(IDefaultCommunicationTimeouts timeouts) : base(timeouts) { }

        public abstract EndpointAddress RemoteAddress { get; }

        public abstract Uri Via { get; }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IOutputChannel))
            {
                return (T)(object)this;
            }

            T baseProperty = base.GetProperty<T>();
            if (baseProperty != null)
            {
                return baseProperty;
            }

            return default(T);
        }

        protected abstract Task OnSendAsync(Message message, CancellationToken token);

        public Task SendAsync(Message message)
        {
            return SendAsync(message, CancellationToken.None);
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            if (message == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));

            // TODO: Fix exception message as a negative timeout wasn't passed, a cancelled token was
            if (token.IsCancellationRequested)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentException(SR.SFxTimeoutOutOfRange0, nameof(token)));

            ThrowIfDisposedOrNotOpen();

            AddHeadersTo(message);
            EmitTrace(message);
            return OnSendAsync(message, token);
        }

        private void EmitTrace(Message message)
        {
        }

        protected virtual void AddHeadersTo(Message message)
        {
        }
    }

    internal abstract class ServiceChannelBase : CommunicationObject, IChannel, IDefaultCommunicationTimeouts
    {
        private IDefaultCommunicationTimeouts _timeouts;

        protected ServiceChannelBase(IDefaultCommunicationTimeouts timeouts)
        {
            _timeouts = new ImmutableCommunicationTimeouts(timeouts);
        }

        TimeSpan IDefaultCommunicationTimeouts.CloseTimeout => DefaultCloseTimeout;

        TimeSpan IDefaultCommunicationTimeouts.OpenTimeout => DefaultOpenTimeout;

        TimeSpan IDefaultCommunicationTimeouts.ReceiveTimeout => DefaultReceiveTimeout;

        TimeSpan IDefaultCommunicationTimeouts.SendTimeout => DefaultSendTimeout;

        protected override TimeSpan DefaultCloseTimeout => _timeouts.CloseTimeout;

        protected override TimeSpan DefaultOpenTimeout => _timeouts.OpenTimeout;

        protected TimeSpan DefaultReceiveTimeout => _timeouts.ReceiveTimeout;

        protected TimeSpan DefaultSendTimeout => _timeouts.SendTimeout;

        public virtual T GetProperty<T>() where T : class
        {
            return null;
        }
    }
}
