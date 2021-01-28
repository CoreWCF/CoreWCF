// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Collections.Generic;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    // This class is now only used as an OM for configuring the service. 
    // This class has been kept to enable using existing behaviors.
    public class ChannelDispatcher : ChannelDispatcherBase
    {
        private readonly ThreadSafeMessageFilterTable<EndpointAddress> _addressTable;
        private EndpointDispatcherCollection _endpointDispatchers;
        private ServiceHostBase _host;

        //bool isTransactedReceive;
        //bool asynchronousTransactedAcceptEnabled;
        //int maxTransactedBatchSize;
        private MessageVersion _messageVersion;
        private bool _receiveSynchronously;
        private bool _sendAsynchronously;
        private int _maxPendingReceives;
        private bool _includeExceptionDetailInFaults;

        //ServiceThrottle serviceThrottle;
        private readonly bool _session;
        private SharedRuntimeState _shared;
        private readonly IDefaultCommunicationTimeouts _timeouts;

        //IsolationLevel transactionIsolationLevel = ServiceBehaviorAttribute.DefaultIsolationLevel;
        //bool transactionIsolationLevelSet;
        private TimeSpan _transactionTimeout;
        private readonly bool _performDefaultCloseInput;

        //EventTraceActivity eventTraceActivity;
        private ErrorBehavior _errorBehavior;

        internal ChannelDispatcher(SharedRuntimeState shared)
        {
            Initialize(shared);
        }

        internal ChannelDispatcher(Uri listenUri, Binding binding, string bindingName, IDefaultCommunicationTimeouts timeouts, List<Type> supportedChannelTypes)
        {
            BindingName = bindingName;
            Binding = binding;
            ListenUri = listenUri;
            SupportedChannelTypes = supportedChannelTypes;
            _timeouts = new ImmutableCommunicationTimeouts(timeouts);
            Initialize(new SharedRuntimeState(true));
        }

        private void Initialize(SharedRuntimeState shared)
        {
            _shared = shared;
            _endpointDispatchers = new EndpointDispatcherCollection(this);
            ChannelInitializers = NewBehaviorCollection<IChannelInitializer>();
            Channels = new CommunicationObjectManager<IChannel>(ThisLock);
            PendingChannels = new SynchronizedChannelCollection<IChannel>(ThisLock);
            ErrorHandlers = new Collection<IErrorHandler>();
            //this.isTransactedReceive = false;
            //this.asynchronousTransactedAcceptEnabled = false;
            _receiveSynchronously = false;
            _sendAsynchronously = true;
            //this.serviceThrottle = null;
            //transactionTimeout = TimeSpan.Zero;
            _maxPendingReceives = 1;
        }

        public string BindingName { get; }

        // TODO: As the channel concept is changing, does it make sense to support IChannelInitializer's?
        public SynchronizedCollection<IChannelInitializer> ChannelInitializers { get; private set; }

        protected override TimeSpan DefaultCloseTimeout
        {
            get
            {
                if (_timeouts != null)
                {
                    return _timeouts.CloseTimeout;
                }
                else
                {
                    return ServiceDefaults.CloseTimeout;
                }
            }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get
            {
                if (_timeouts != null)
                {
                    return _timeouts.OpenTimeout;
                }
                else
                {
                    return ServiceDefaults.OpenTimeout;
                }
            }
        }

        internal EndpointDispatcherTable EndpointDispatcherTable { get; private set; }

        internal CommunicationObjectManager<IChannel> Channels { get; private set; }

        public SynchronizedCollection<EndpointDispatcher> Endpoints
        {
            get { return _endpointDispatchers; }
        }

        public Collection<IErrorHandler> ErrorHandlers { get; private set; }

        public MessageVersion MessageVersion
        {
            get { return _messageVersion; }
            set
            {
                _messageVersion = value;
                ThrowIfDisposedOrImmutable();
            }
        }

        public override ServiceHostBase Host
        {
            get { return _host; }
        }

        internal bool EnableFaults
        {
            get { return _shared.EnableFaults; }
            set
            {
                ThrowIfDisposedOrImmutable();
                _shared.EnableFaults = value;
            }
        }

        //public ServiceThrottle ServiceThrottle
        //{
        //    get
        //    {
        //        return this.serviceThrottle;
        //    }
        //    set
        //    {
        //        this.ThrowIfDisposedOrImmutable();
        //        this.serviceThrottle = value;
        //    }
        //}

        public bool ManualAddressing
        {
            get { return _shared.ManualAddressing; }
            set
            {
                ThrowIfDisposedOrImmutable();
                _shared.ManualAddressing = value;
            }
        }

        internal SynchronizedChannelCollection<IChannel> PendingChannels { get; private set; }

        public bool ReceiveSynchronously
        {
            get
            {
                return _receiveSynchronously;
            }
            set
            {
                ThrowIfDisposedOrImmutable();
                if (value != false)
                {
                    throw new ArgumentException("Only false supported", nameof(ReceiveSynchronously));
                }

                _receiveSynchronously = value;
            }
        }

        public bool SendAsynchronously
        {
            get
            {
                return _sendAsynchronously;
            }
            set
            {
                ThrowIfDisposedOrImmutable();
                if (value != true)
                {
                    throw new ArgumentException("Only true supported", nameof(ReceiveSynchronously));
                }

                _sendAsynchronously = value;
            }
        }

        // TODO: Do we need to worry about this?
        public int MaxPendingReceives
        {
            get
            {
                return _maxPendingReceives;
            }
            set
            {
                ThrowIfDisposedOrImmutable();
                _maxPendingReceives = value;
            }
        }

        public bool IncludeExceptionDetailInFaults
        {
            get { return _includeExceptionDetailInFaults; }
            set
            {
                lock (ThisLock)
                {
                    ThrowIfDisposedOrImmutable();
                    _includeExceptionDetailInFaults = value;
                }
            }
        }

        internal Uri ListenUri { get; }

        internal List<Type> SupportedChannelTypes { get; }

        internal Binding Binding { get; }

        internal bool HandleError(Exception error)
        {
            ErrorHandlerFaultInfo dummy = new ErrorHandlerFaultInfo();
            return HandleError(error, ref dummy);
        }

        internal bool HandleError(Exception error, ref ErrorHandlerFaultInfo faultInfo)
        {
            ErrorBehavior behavior;

            lock (ThisLock)
            {
                if (_errorBehavior != null)
                {
                    behavior = _errorBehavior;
                }
                else
                {
                    behavior = new ErrorBehavior(this);
                }
            }

            if (behavior != null)
            {
                return behavior.HandleError(error, ref faultInfo);
            }
            else
            {
                return false;
            }
        }

        internal void InitializeChannel(IClientChannel channel)
        {
            ThrowIfDisposedOrNotOpen();
            try
            {
                for (int i = 0; i < ChannelInitializers.Count; ++i)
                {
                    ChannelInitializers[i].Initialize(channel);
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
            }
        }

        internal void Init()
        {
            _errorBehavior = new ErrorBehavior(this);

            EndpointDispatcherTable = new EndpointDispatcherTable(ThisLock);
            for (int i = 0; i < _endpointDispatchers.Count; i++)
            {
                EndpointDispatcher endpoint = _endpointDispatchers[i];

                // Force a build of the runtime to catch any unexpected errors before we are done opening.
                endpoint.DispatchRuntime.GetRuntime();
                // Lock down the DispatchRuntime.
                endpoint.DispatchRuntime.LockDownProperties();

                EndpointDispatcherTable.AddEndpoint(endpoint);

                if ((_addressTable != null) && (endpoint.OriginalAddress != null))
                {
                    _addressTable.Add(endpoint.AddressFilter, endpoint.OriginalAddress, endpoint.FilterPriority);
                }

                //if (DiagnosticUtility.ShouldTraceInformation)
                //{
                //    this.TraceEndpointLifetime(endpoint, TraceCode.EndpointListenerOpen, SR.Format(SR.TraceCodeEndpointListenerOpen));
                //}
            }
        }

        internal SynchronizedCollection<T> NewBehaviorCollection<T>()
        {
            return new ChannelDispatcherBehaviorCollection<T>(this);
        }

        internal bool HasApplicationEndpoints
        {
            get
            {
                foreach (EndpointDispatcher endpointDispatcher in Endpoints)
                {
                    if (!endpointDispatcher.IsSystemEndpoint)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private void OnAddEndpoint(EndpointDispatcher endpoint)
        {
            lock (ThisLock)
            {
                endpoint.Attach(this);

                if (State == CommunicationState.Opened)
                {
                    if (_addressTable != null)
                    {
                        _addressTable.Add(endpoint.AddressFilter, endpoint.EndpointAddress, endpoint.FilterPriority);
                    }

                    EndpointDispatcherTable.AddEndpoint(endpoint);
                }
            }
        }

        private void OnRemoveEndpoint(EndpointDispatcher endpoint)
        {
            lock (ThisLock)
            {
                if (State == CommunicationState.Opened)
                {
                    EndpointDispatcherTable.RemoveEndpoint(endpoint);

                    if (_addressTable != null)
                    {
                        _addressTable.Remove(endpoint.AddressFilter);
                    }
                }

                endpoint.Detach(this);
            }
        }

        protected override void OnAbort()
        {
            throw new PlatformNotSupportedException();
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            throw new PlatformNotSupportedException();
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        // TODO: Move this functionality somewhere else as this class is now OM only
        internal void ProvideFault(Exception e, FaultConverter faultConverter, ref ErrorHandlerFaultInfo faultInfo)
        {
            ErrorBehavior behavior;

            lock (ThisLock)
            {
                if (_errorBehavior != null)
                {
                    behavior = _errorBehavior;
                }
                else
                {
                    behavior = new ErrorBehavior(this);
                }
            }

            behavior.ProvideFault(e, faultConverter, ref faultInfo);
        }

        protected override void Attach(ServiceHostBase host)
        {
            if (host == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(host));
            }

            ServiceHostBase serviceHost = host;

            ThrowIfDisposedOrImmutable();

            if (_host != null)
            {
                Exception error = new InvalidOperationException(SR.SFxChannelDispatcherMultipleHost0);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }

            _host = serviceHost;
        }

        protected override void Detach(ServiceHostBase host)
        {
            if (host == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(host));
            }

            if (_host != host)
            {
                Exception error = new InvalidOperationException(SR.SFxChannelDispatcherDifferentHost0);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }

            ThrowIfDisposedOrImmutable();

            _host = null;
        }

        private class EndpointDispatcherCollection : SynchronizedCollection<EndpointDispatcher>
        {
            private readonly ChannelDispatcher _owner;

            internal EndpointDispatcherCollection(ChannelDispatcher owner)
                : base(owner.ThisLock)
            {
                _owner = owner;
            }

            protected override void ClearItems()
            {
                foreach (EndpointDispatcher item in Items)
                {
                    _owner.OnRemoveEndpoint(item);
                }
                base.ClearItems();
            }

            protected override void InsertItem(int index, EndpointDispatcher item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                _owner.OnAddEndpoint(item);
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                EndpointDispatcher item = Items[index];
                base.RemoveItem(index);
                _owner.OnRemoveEndpoint(item);
            }

            protected override void SetItem(int index, EndpointDispatcher item)
            {
                Exception error = new InvalidOperationException(SR.SFxCollectionDoesNotSupportSet0);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }
        }

        private class ChannelDispatcherBehaviorCollection<T> : SynchronizedCollection<T>
        {
            private readonly ChannelDispatcher _outer;

            internal ChannelDispatcherBehaviorCollection(ChannelDispatcher outer)
                : base(outer.ThisLock)
            {
                _outer = outer;
            }

            protected override void ClearItems()
            {
                _outer.ThrowIfDisposedOrImmutable();
                base.ClearItems();
            }

            protected override void InsertItem(int index, T item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                _outer.ThrowIfDisposedOrImmutable();
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                _outer.ThrowIfDisposedOrImmutable();
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, T item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                _outer.ThrowIfDisposedOrImmutable();
                base.SetItem(index, item);
            }
        }
    }
}