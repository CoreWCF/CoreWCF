using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;
using System.Diagnostics;

namespace CoreWCF
{
    public sealed class InstanceContext : CommunicationObject, IExtensibleObject<InstanceContext>
    {
        internal static Action<InstanceContext> NotifyEmptyCallback = NotifyEmpty;
        internal static Action<InstanceContext> NotifyIdleCallback = NotifyIdle;

        private bool _autoClose;
        private InstanceBehavior _behavior;
        private ServiceChannelManager _channels;
        private ConcurrencyInstanceContextFacet _concurrency;
        private ExtensionCollection<InstanceContext> _extensions;
        private readonly ServiceHostBase _host;
        ServiceThrottle serviceThrottle;
        private int _instanceContextManagerIndex;
        //int instanceContextManagerIndex;
        private object _serviceInstanceLock = new object();
        private SynchronizationContext _synchronizationContext;
        //TransactionInstanceContextFacet transaction;
        private object _userObject;
        private bool _wellKnown;
        //SynchronizedCollection<IChannel> wmiChannels;
        private bool _isUserCreated;

        public InstanceContext(object implementation)
            : this(null, implementation)
        {
        }

        public InstanceContext(ServiceHostBase host, object implementation)
            : this(host, implementation, true)
        {
        }

        internal InstanceContext(ServiceHostBase host, object implementation, bool isUserCreated)
            : this(host, implementation, true, isUserCreated)
        {
        }

        internal InstanceContext(ServiceHostBase host, object implementation, bool wellKnown, bool isUserCreated)
        {
            _host = host;
            if (implementation != null)
            {
                _userObject = implementation;
                _wellKnown = wellKnown;
            }
            _autoClose = false;
            _channels = new ServiceChannelManager(this);
            _isUserCreated = isUserCreated;
        }

        internal InstanceContext(ServiceHostBase host, bool isUserCreated)
        {
            // TODO: Why is it required that host != null?
            //if (host == null)
            //{
            //    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(host));
            //}

            _host = host;
            _autoClose = true;
            _channels = new ServiceChannelManager(this, NotifyEmptyCallback);
            _isUserCreated = isUserCreated;
        }

        internal bool IsUserCreated
        {
            get { return _isUserCreated; }
            set { _isUserCreated = value; }
        }

        internal bool IsWellKnown
        {
            get { return _wellKnown; }
        }

        internal bool AutoClose
        {
            get { return _autoClose; }
            set { _autoClose = value; }
        }

        internal InstanceBehavior Behavior
        {
            get { return _behavior; }
            set
            {
                if (_behavior == null)
                {
                    _behavior = value;
                }
            }
        }

        internal ConcurrencyInstanceContextFacet Concurrency
        {
            get
            {
                if (_concurrency == null)
                {
                    lock (ThisLock)
                    {
                        if (_concurrency == null)
                            _concurrency = new ConcurrencyInstanceContextFacet();
                    }
                }

                return _concurrency;
            }
        }

        protected override TimeSpan DefaultCloseTimeout
        {
            get
            {
                if (_host != null)
                {
                    return _host.CloseTimeout;
                }
                return ServiceDefaults.CloseTimeout;
            }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get
            {
                if (_host != null)
                {
                    return _host.OpenTimeout;
                }
                return ServiceDefaults.OpenTimeout;
            }
        }

        public IExtensionCollection<InstanceContext> Extensions
        {
            get
            {
                ThrowIfClosed();
                lock (ThisLock)
                {
                    if (_extensions == null)
                        _extensions = new ExtensionCollection<InstanceContext>(this, ThisLock);
                    return _extensions;
                }
            }
        }

        internal ICollection<IChannel> IncomingChannels
        {
            get
            {
                ThrowIfClosed();
                return _channels.IncomingChannels;
            }
        }

        private bool IsBusy
        {
            get
            {
                if (State == CommunicationState.Closed)
                    return false;
                return _channels.IsBusy;
            }
        }

        bool IsSingleton
        {
            get
            {
                return ((_behavior != null) &&
                        InstanceContextProviderBase.IsProviderSingleton(_behavior.InstanceContextProvider));
            }
        }

        internal ICollection<IChannel> OutgoingChannels
        {
            get
            {
                ThrowIfClosed();
                return _channels.OutgoingChannels;
            }
        }

        internal ServiceHostBase Host
        {
            get
            {
                ThrowIfClosed();
                return _host;
            }
        }

        public int ManualFlowControlLimit
        {
            get => EnsureQuotaThrottle().Limit;
            set => EnsureQuotaThrottle().SetLimit(value);
        }

        internal QuotaThrottle QuotaThrottle { get; private set; }

        internal ServiceThrottle ServiceThrottle
        {
            get { return serviceThrottle; }
            set
            {
                ThrowIfDisposed();
                serviceThrottle = value;
            }
        }

        internal int InstanceContextManagerIndex
        {
            get { return _instanceContextManagerIndex; }
            set { _instanceContextManagerIndex = value; }
        }
        
        public SynchronizationContext SynchronizationContext
        {
            get { return _synchronizationContext; }
            set
            {
                ThrowIfClosedOrOpened();
                _synchronizationContext = value;
            }
        }

        internal new object ThisLock
        {
            get { return base.ThisLock; }
        }

        protected override void OnAbort()
        {
            _channels.Abort();
            Unload();
        }

        internal Task CloseInputAsync(CancellationToken token)
        {
            return _channels.CloseInputAsync(token);
        }

        internal void BindRpc(MessageRpc rpc)
        {
            ThrowIfClosed();
            _channels.IncrementActivityCount();
            rpc.SuccessfullyBoundInstance = true;
        }

        internal void BindIncomingChannel(ServiceChannel channel)
        {
            ThrowIfDisposed();

            channel.InstanceContext = this;
            IChannel proxy = (IChannel)channel.Proxy;
            _channels.AddIncomingChannel(proxy);

            // CSDMain 265783: Memory Leak on Chat Stress test scenario
            // There's a race condition while on one thread we received a new request from underlying sessionful channel
            // and on another thread we just aborted the channel. So the channel will be added to the IncomingChannels list of 
            // ServiceChannelManager and never get a chance to be removed.
            if (proxy != null)
            {
                CommunicationState state = channel.State;
                if (state == CommunicationState.Closing
                    || state == CommunicationState.Closed
                    || state == CommunicationState.Faulted)
                {
                    _channels.RemoveChannel(proxy);
                }
            }
        }

        private void CloseIfNotBusy()
        {
            if (!(State != CommunicationState.Created && State != CommunicationState.Opening))
            {
                Fx.Assert(
                    "InstanceContext.CloseIfNotBusy: (this.State != CommunicationState.Created && this.State != CommunicationState.Opening)");
            }

            if (State != CommunicationState.Opened)
                return;

            if (IsBusy)
                return;

            if (_behavior.CanUnload(this) == false)
                return;

            try
            {
                // TODO: Make this call and it's chain async
                if (State == CommunicationState.Opened)
                    CloseAsync().GetAwaiter().GetResult();
            }
            catch (ObjectDisposedException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (InvalidOperationException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (CommunicationException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (TimeoutException e)
            {
                //if (TD.CloseTimeoutIsEnabled())
                //{
                //    TD.CloseTimeout(e.Message);
                //}
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
        }

        QuotaThrottle EnsureQuotaThrottle()
        {
            lock (ThisLock)
            {
                if (QuotaThrottle == null)
                {
                    QuotaThrottle = new QuotaThrottle(ThisLock);
                    QuotaThrottle.Owner = "InstanceContext";
                }
                return QuotaThrottle;
            }
        }

        internal void FaultInternal()
        {
            Fault();
        }

        public object GetServiceInstance()
        {
            return GetServiceInstance(null);
        }

        public object GetServiceInstance(Message message)
        {
            lock (_serviceInstanceLock)
            {
                ThrowIfClosedOrNotOpen();

                object current = _userObject;

                if (current != null)
                {
                    return current;
                }

                if (_behavior == null)
                {
                    Exception error = new InvalidOperationException(SR.SFxInstanceNotInitialized);
                    if (message != null)
                    {
                        throw TraceUtility.ThrowHelperError(error, message);
                    }
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
                }

                object newUserObject;
                if (message != null)
                {
                    newUserObject = _behavior.GetInstance(this, message);
                }
                else
                {
                    newUserObject = _behavior.GetInstance(this);
                }
                if (newUserObject != null)
                {
                    SetUserObject(newUserObject);
                }

                return newUserObject;
            }
        }

        private void Load()
        {
            if (_behavior != null)
            {
                _behavior.Initialize(this);
            }

            // TODO: Re-home InstanceContextManager and have a reference to it here.
            //if (_host != null)
            //{
            //    _host.BindInstance(this);
            //}
        }

        private static void NotifyEmpty(InstanceContext instanceContext)
        {
            if (instanceContext._autoClose)
            {
                instanceContext.CloseIfNotBusy();
            }
        }

        private static void NotifyIdle(InstanceContext instanceContext)
        {
            instanceContext.CloseIfNotBusy();
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            await _channels.CloseAsync(token);
            Unload();
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            serviceThrottle?.DeactivateInstanceContext();
        }

        protected override void OnFaulted()
        {
            base.OnFaulted();

            if (IsSingleton && (_host != null))
            {
                // TODO: Create new mechanism to register fault event handlers
                //_host.FaultInternal();
            }
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override void OnOpened()
        {
            base.OnOpened();
        }

        protected override void OnOpening()
        {
            Load();
            base.OnOpening();
        }


        internal void ReleaseServiceInstance()
        {
            ThrowIfDisposedOrNotOpen();
            SetUserObject(null);
        }

        private void SetUserObject(object newUserObject)
        {
            if (_behavior != null && !_wellKnown)
            {
                object oldUserObject = Interlocked.Exchange(ref _userObject, newUserObject);

                // TODO: Make DisposableInstance accessible via some other mechanism
                //if ((oldUserObject != null) && (_host != null) && !Equals(oldUserObject, _host.DisposableInstance))
                //{
                //    _behavior.ReleaseInstance(this, oldUserObject);
                //}
            }
        }

        internal void UnbindRpc(ref MessageRpc rpc)
        {
            if (rpc.InstanceContext == this && rpc.SuccessfullyBoundInstance)
            {
                _channels.DecrementActivityCount();
            }
        }

        void Unload()
        {
            SetUserObject(null);

            // TODO: Re-home InstanceContextManager and have a reference to it here.
            //if (_host != null)
            //{
            //    _host.UnbindInstance(this);
            //}
        }
    }
}