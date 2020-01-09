using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Collections.Generic;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;

namespace CoreWCF.Dispatcher
{
    // This class is now only used as an OM for configuring the service. 
    // This class has been kept to enable using existing behaviors.
    public class ChannelDispatcher : ChannelDispatcherBase
    {
        ThreadSafeMessageFilterTable<EndpointAddress> addressTable;
        CommunicationObjectManager<IChannel> channels;
        EndpointDispatcherCollection endpointDispatchers;
        EndpointDispatcherTable filterTable;
        ServiceHostBase host;
        //bool isTransactedReceive;
        //bool asynchronousTransactedAcceptEnabled;
        //int maxTransactedBatchSize;
        MessageVersion messageVersion;
        bool receiveSynchronously;
        bool sendAsynchronously;
        int maxPendingReceives;
        bool includeExceptionDetailInFaults;
        //ServiceThrottle serviceThrottle;
        bool session;
        SharedRuntimeState shared;
        IDefaultCommunicationTimeouts timeouts;
        //IsolationLevel transactionIsolationLevel = ServiceBehaviorAttribute.DefaultIsolationLevel;
        //bool transactionIsolationLevelSet;
        TimeSpan transactionTimeout;
        bool performDefaultCloseInput;
        //EventTraceActivity eventTraceActivity;
        ErrorBehavior errorBehavior;

        internal ChannelDispatcher(SharedRuntimeState shared)
        {
            Initialize(shared);
        }

        internal ChannelDispatcher(Uri listenUri, Binding binding, string bindingName, IDefaultCommunicationTimeouts timeouts)
        {
            BindingName = bindingName;
            Binding = binding;
            ListenUri = listenUri;
            this.timeouts = new ImmutableCommunicationTimeouts(timeouts);
            Initialize(new SharedRuntimeState(true));
        }

        void Initialize(SharedRuntimeState shared)
        {
            this.shared = shared;
            endpointDispatchers = new EndpointDispatcherCollection(this);
            ChannelInitializers = NewBehaviorCollection<IChannelInitializer>();
            channels = new CommunicationObjectManager<IChannel>(ThisLock);
            PendingChannels = new SynchronizedChannelCollection<IChannel>(ThisLock);
            ErrorHandlers = new Collection<IErrorHandler>();
            //this.isTransactedReceive = false;
            //this.asynchronousTransactedAcceptEnabled = false;
            receiveSynchronously = false;
            sendAsynchronously = true;
            //this.serviceThrottle = null;
            //transactionTimeout = TimeSpan.Zero;
            maxPendingReceives = 1;
        }

        public string BindingName { get; }

        // TODO: As the channel concept is changing, does it make sense to support IChannelInitializer's?
        public SynchronizedCollection<IChannelInitializer> ChannelInitializers { get; private set; }

        protected override TimeSpan DefaultCloseTimeout
        {
            get
            {
                if (timeouts != null)
                {
                    return timeouts.CloseTimeout;
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
                if (timeouts != null)
                {
                    return timeouts.OpenTimeout;
                }
                else
                {
                    return ServiceDefaults.OpenTimeout;
                }
            }
        }

        internal EndpointDispatcherTable EndpointDispatcherTable
        {
            get { return filterTable; }
        }

        internal CommunicationObjectManager<IChannel> Channels
        {
            get { return channels; }
        }

        public SynchronizedCollection<EndpointDispatcher> Endpoints
        {
            get { return endpointDispatchers; }
        }

        public Collection<IErrorHandler> ErrorHandlers { get; private set; }

        public MessageVersion MessageVersion
        {
            get { return messageVersion; }
            set
            {
                messageVersion = value;
                ThrowIfDisposedOrImmutable();
            }
        }

        public override ServiceHostBase Host
        {
            get { return host; }
        }

        internal bool EnableFaults
        {
            get { return shared.EnableFaults; }
            set
            {
                ThrowIfDisposedOrImmutable();
                shared.EnableFaults = value;
            }
        }

        internal bool BufferedReceiveEnabled
        {
            get;
            set;
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
            get { return shared.ManualAddressing; }
            set
            {
                ThrowIfDisposedOrImmutable();
                shared.ManualAddressing = value;
            }
        }

        internal SynchronizedChannelCollection<IChannel> PendingChannels { get; private set; }

        public bool ReceiveSynchronously
        {
            get
            {
                return receiveSynchronously;
            }
            set
            {
                ThrowIfDisposedOrImmutable();
                if (value != false)
                {
                    throw new ArgumentException("Only false supported", nameof(ReceiveSynchronously));
                }

                receiveSynchronously = value;
            }
        }

        public bool SendAsynchronously
        {
            get
            {
                return sendAsynchronously;
            }
            set
            {
                ThrowIfDisposedOrImmutable();
                if (value != true)
                {
                    throw new ArgumentException("Only true supported", nameof(ReceiveSynchronously));
                }

                sendAsynchronously = value;
            }

        }

        // TODO: Do we need to worry about this?
        public int MaxPendingReceives
        {
            get
            {
                return maxPendingReceives;
            }
            set
            {
                ThrowIfDisposedOrImmutable();
                maxPendingReceives = value;
            }
        }

        public bool IncludeExceptionDetailInFaults
        {
            get { return includeExceptionDetailInFaults; }
            set
            {
                lock (ThisLock)
                {
                    ThrowIfDisposedOrImmutable();
                    includeExceptionDetailInFaults = value;
                }
            }
        }

        internal Uri ListenUri { get; }

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
                if (errorBehavior != null)
                {
                    behavior = errorBehavior;
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
            errorBehavior = new ErrorBehavior(this);

            filterTable = new EndpointDispatcherTable(ThisLock);
            for (int i = 0; i < endpointDispatchers.Count; i++)
            {
                EndpointDispatcher endpoint = endpointDispatchers[i];

                // Force a build of the runtime to catch any unexpected errors before we are done opening.
                endpoint.DispatchRuntime.GetRuntime();
                // Lock down the DispatchRuntime.
                endpoint.DispatchRuntime.LockDownProperties();

                filterTable.AddEndpoint(endpoint);

                if ((addressTable != null) && (endpoint.OriginalAddress != null))
                {
                    addressTable.Add(endpoint.AddressFilter, endpoint.OriginalAddress, endpoint.FilterPriority);
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

        void OnAddEndpoint(EndpointDispatcher endpoint)
        {
            lock (ThisLock)
            {
                endpoint.Attach(this);

                if (State == CommunicationState.Opened)
                {
                    if (addressTable != null)
                    {
                        addressTable.Add(endpoint.AddressFilter, endpoint.EndpointAddress, endpoint.FilterPriority);
                    }

                    filterTable.AddEndpoint(endpoint);
                }
            }
        }

        void OnRemoveEndpoint(EndpointDispatcher endpoint)
        {
            lock (ThisLock)
            {
                if (State == CommunicationState.Opened)
                {
                    filterTable.RemoveEndpoint(endpoint);

                    if (addressTable != null)
                    {
                        addressTable.Remove(endpoint.AddressFilter);
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
                if (errorBehavior != null)
                {
                    behavior = errorBehavior;
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

            if (this.host != null)
            {
                Exception error = new InvalidOperationException(SR.SFxChannelDispatcherMultipleHost0);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }

            this.host = serviceHost;
        }

        protected override void Detach(ServiceHostBase host)
        {
            if (host == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(host));
            }

            if (this.host != host)
            {
                Exception error = new InvalidOperationException(SR.SFxChannelDispatcherDifferentHost0);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }

            ThrowIfDisposedOrImmutable();

            this.host = null;
        }

        class EndpointDispatcherCollection : SynchronizedCollection<EndpointDispatcher>
        {
            ChannelDispatcher owner;

            internal EndpointDispatcherCollection(ChannelDispatcher owner)
                : base(owner.ThisLock)
            {
                this.owner = owner;
            }

            protected override void ClearItems()
            {
                foreach (EndpointDispatcher item in Items)
                {
                    owner.OnRemoveEndpoint(item);
                }
                base.ClearItems();
            }

            protected override void InsertItem(int index, EndpointDispatcher item)
            {
                if (item == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));

                owner.OnAddEndpoint(item);
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                EndpointDispatcher item = Items[index];
                base.RemoveItem(index);
                owner.OnRemoveEndpoint(item);
            }

            protected override void SetItem(int index, EndpointDispatcher item)
            {
                Exception error = new InvalidOperationException(SR.SFxCollectionDoesNotSupportSet0);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }
        }

        class ChannelDispatcherBehaviorCollection<T> : SynchronizedCollection<T>
        {
            ChannelDispatcher outer;

            internal ChannelDispatcherBehaviorCollection(ChannelDispatcher outer)
                : base(outer.ThisLock)
            {
                this.outer = outer;
            }

            protected override void ClearItems()
            {
                outer.ThrowIfDisposedOrImmutable();
                base.ClearItems();
            }

            protected override void InsertItem(int index, T item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                outer.ThrowIfDisposedOrImmutable();
                base.InsertItem(index, item);
            }

            protected override void RemoveItem(int index)
            {
                outer.ThrowIfDisposedOrImmutable();
                base.RemoveItem(index);
            }

            protected override void SetItem(int index, T item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

                outer.ThrowIfDisposedOrImmutable();
                base.SetItem(index, item);
            }
        }
    }

}