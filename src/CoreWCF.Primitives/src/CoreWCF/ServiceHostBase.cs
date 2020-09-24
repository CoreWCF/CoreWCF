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
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.Dispatcher;
using System.Diagnostics;

namespace CoreWCF
{
    public abstract class ServiceHostBase : CommunicationObject, IExtensibleObject<ServiceHostBase>, IDisposable
    {
        internal static readonly Uri EmptyUri = new Uri(string.Empty, UriKind.RelativeOrAbsolute);

        bool initializeDescriptionHasFinished;
        UriSchemeKeyedCollection baseAddresses;
        ChannelDispatcherCollection channelDispatchers;
        TimeSpan closeTimeout = ServiceDefaults.ServiceHostCloseTimeout;
        ServiceDescription description;
        ExtensionCollection<ServiceHostBase> extensions;
        ReadOnlyCollection<Uri> externalBaseAddresses;
        IDictionary<string, ContractDescription> implementedContracts;
        IInstanceContextManager instances;
        TimeSpan openTimeout = ServiceDefaults.OpenTimeout;
        ServiceCredentials readOnlyCredentials;
        ServiceAuthorizationBehavior readOnlyAuthorization;
        //ServiceAuthenticationBehavior readOnlyAuthentication;
        Dictionary<DispatcherBuilder.ListenUriInfo, Collection<ServiceEndpoint>> endpointsByListenUriInfo;
        int busyCount;
        //EventTraceActivity eventTraceActivity;

        public event EventHandler<UnknownMessageReceivedEventArgs> UnknownMessageReceived;

        protected ServiceHostBase()
        {
            baseAddresses = new UriSchemeKeyedCollection(ThisLock);
            channelDispatchers = new ChannelDispatcherCollection(this, ThisLock);
            extensions = new ExtensionCollection<ServiceHostBase>(this, ThisLock);
            instances = new InstanceContextManager(ThisLock);
        }

        public ServiceAuthorizationBehavior Authorization
        {
            get
            {
                if (Description == null)
                {
                    return null;
                }
                else if (State == CommunicationState.Created || State == CommunicationState.Opening)
                {
                    return EnsureAuthorization(Description);
                }
                else
                {
                    return readOnlyAuthorization;
                }
            }
        }

        // TODO: Bring in ServiceAuthenticationBehavior
        //public ServiceAuthenticationBehavior Authentication
        //{
        //    get
        //    {
        //        if (this.Description == null)
        //        {
        //            return null;
        //        }
        //        else if (this.State == CommunicationState.Created || this.State == CommunicationState.Opening)
        //        {
        //            return EnsureAuthentication(this.Description);
        //        }
        //        else
        //        {
        //            return this.readOnlyAuthentication;
        //        }
        //    }
        //}

        public ReadOnlyCollection<Uri> BaseAddresses
        {
            get
            {
                externalBaseAddresses = new ReadOnlyCollection<Uri>(new List<Uri>(baseAddresses));
                return externalBaseAddresses;
            }
        }

        public ChannelDispatcherCollection ChannelDispatchers
        {
            get { return channelDispatchers; }
        }

        public TimeSpan CloseTimeout
        {
            get { return closeTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    string message = SR.SFxTimeoutOutOfRange0;
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", message));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", SR.SFxTimeoutOutOfRangeTooBig));
                }

                lock (ThisLock)
                {
                    ThrowIfClosedOrOpened();
                    closeTimeout = value;
                }
            }
        }

        public ServiceCredentials Credentials
        {
            get
            {
                if (Description == null)
                {
                    return null;
                }
                else if (State == CommunicationState.Created || State == CommunicationState.Opening)
                {
                    return EnsureCredentials(Description);
                }
                else
                {
                    return readOnlyCredentials;
                }
            }
        }

        protected override TimeSpan DefaultCloseTimeout
        {
            get { return CloseTimeout; }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get { return OpenTimeout; }
        }

        public ServiceDescription Description
        {
            get { return description; }
        }

        public IExtensionCollection<ServiceHostBase> Extensions
        {
            get { return extensions; }
        }

        protected internal IDictionary<string, ContractDescription> ImplementedContracts
        {
            get { return implementedContracts; }
        }

        internal UriSchemeKeyedCollection InternalBaseAddresses
        {
            get { return baseAddresses; }
        }

        public int ManualFlowControlLimit
        {
            get { return 0; /*return this.ServiceThrottle.ManualFlowControlLimit;*/ }
            set { /*this.ServiceThrottle.ManualFlowControlLimit = value;*/ }
        }

        public TimeSpan OpenTimeout
        {
            get { return openTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    string message = SR.SFxTimeoutOutOfRange0;
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", message));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("value", SR.SFxTimeoutOutOfRangeTooBig));
                }

                lock (ThisLock)
                {
                    ThrowIfClosedOrOpened();
                    openTimeout = value;
                }
            }
        }

        internal virtual object DisposableInstance
        {
            get
            {
                return null;
            }
        }

        protected void AddBaseAddress(Uri baseAddress)
        {
            if (initializeDescriptionHasFinished)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.SFxCannotCallAddBaseAddress));
            }
            baseAddresses.Add(baseAddress);
        }

        public ServiceEndpoint AddServiceEndpoint(string implementedContract, Binding binding, string address)
        {
            throw new PlatformNotSupportedException();
        }

        public ServiceEndpoint AddServiceEndpoint(string implementedContract, Binding binding, string address, Uri listenUri)
        {
            throw new PlatformNotSupportedException();
        }

        public ServiceEndpoint AddServiceEndpoint(string implementedContract, Binding binding, Uri address)
        {
            throw new PlatformNotSupportedException();
        }

        public ServiceEndpoint AddServiceEndpoint(string implementedContract, Binding binding, Uri address, Uri listenUri)
        {
            throw new PlatformNotSupportedException();
        }

        public virtual void AddServiceEndpoint(ServiceEndpoint endpoint)
        {
            throw new PlatformNotSupportedException();
        }

        public void SetEndpointAddress(ServiceEndpoint endpoint, string relativeAddress)
        {
            throw new PlatformNotSupportedException();
        }

        protected virtual void ApplyConfiguration()
        {
            throw new PlatformNotSupportedException();
        }

        public virtual ReadOnlyCollection<ServiceEndpoint> AddDefaultEndpoints()
        {
            throw new PlatformNotSupportedException();
        }

        internal virtual void BindInstance(InstanceContext instance)
        {
            instances.Add(instance);
            //if (null != this.servicePerformanceCounters)
            //{
            //    lock (this.ThisLock)
            //    {
            //        if (null != this.servicePerformanceCounters)
            //        {
            //            this.servicePerformanceCounters.ServiceInstanceCreated();
            //        }
            //    }
            //}
        }

        void IDisposable.Dispose()
        {
            CloseAsync().GetAwaiter().GetResult();
        }

        protected abstract ServiceDescription CreateDescription(out IDictionary<string, ContractDescription> implementedContracts);

        protected virtual void InitializeRuntime()
        {
            throw new PlatformNotSupportedException();
        }

        ServiceAuthorizationBehavior EnsureAuthorization(ServiceDescription description)
        {
            Fx.Assert(State == CommunicationState.Created || State == CommunicationState.Opening, "");
            ServiceAuthorizationBehavior a = description.Behaviors.Find<ServiceAuthorizationBehavior>();

            if (a == null)
            {
                a = new ServiceAuthorizationBehavior();
                description.Behaviors.Add(a);
            }

            return a;
        }

        //ServiceAuthenticationBehavior EnsureAuthentication(ServiceDescription description)
        //{
        //    Fx.Assert(this.State == CommunicationState.Created || this.State == CommunicationState.Opening, "");
        //    ServiceAuthenticationBehavior a = description.Behaviors.Find<ServiceAuthenticationBehavior>();

        //    if (a == null)
        //    {
        //        a = new ServiceAuthenticationBehavior();
        //        description.Behaviors.Add(a);
        //    }
        //    return a;
        //}

        ServiceCredentials EnsureCredentials(ServiceDescription description)
        {
            Fx.Assert(State == CommunicationState.Created || State == CommunicationState.Opening, "");
            ServiceCredentials c = description.Behaviors.Find<ServiceCredentials>();

            if (c == null)
            {
                c = new ServiceCredentials();
                description.Behaviors.Add(c);
            }

            return c;
        }

        public int IncrementManualFlowControlLimit(int incrementBy)
        {
            throw new PlatformNotSupportedException();
        }

        protected void InitializeDescription(UriSchemeKeyedCollection baseAddresses)
        {
            foreach (Uri baseAddress in baseAddresses)
            {
                this.baseAddresses.Add(baseAddress);
            }

            description = CreateDescription(out implementedContracts);
            ApplyConfiguration();
            initializeDescriptionHasFinished = true;
        }

        // Configuration
        //protected void LoadConfigurationSection(ServiceElement serviceSection)
        //{
        //    throw new PlatformNotSupportedException();
        //}

        internal void OnAddChannelDispatcher(ChannelDispatcherBase channelDispatcher)
        {
            lock (ThisLock)
            {
                ThrowIfClosedOrOpened();
                channelDispatcher.AttachInternal(this);
                channelDispatcher.Faulted += new EventHandler(OnChannelDispatcherFaulted);
            }
        }

        internal void OnRemoveChannelDispatcher(ChannelDispatcherBase channelDispatcher)
        {
            lock (ThisLock)
            {
                ThrowIfClosedOrOpened();
                channelDispatcher.DetachInternal(this);
            }
        }

        void OnChannelDispatcherFaulted(object sender, EventArgs e)
        {
            Fault();
        }

        protected void ReleasePerformanceCounters()
        {
            throw new PlatformNotSupportedException();
        }

        internal virtual void UnbindInstance(InstanceContext instance)
        {
            instances.Remove(instance);
            //if (null != this.servicePerformanceCounters)
            //{
            //    lock (this.ThisLock)
            //    {
            //        if (null != this.servicePerformanceCounters)
            //        {
            //            this.servicePerformanceCounters.ServiceInstanceRemoved();
            //        }
            //    }
            //}
        }

        class ImplementedContractsContractResolver : IContractResolver
        {
            IDictionary<string, ContractDescription> implementedContracts;

            public ImplementedContractsContractResolver(IDictionary<string, ContractDescription> implementedContracts)
            {
                this.implementedContracts = implementedContracts;
            }

            public ContractDescription ResolveContract(string contractName)
            {
                return implementedContracts != null && implementedContracts.ContainsKey(contractName) ? implementedContracts[contractName] : null;
            }
        }

        internal class ServiceAndBehaviorsContractResolver : IContractResolver
        {
            IContractResolver serviceResolver;
            Dictionary<string, ContractDescription> behaviorContracts;

            public Dictionary<string, ContractDescription> BehaviorContracts
            {
                get { return behaviorContracts; }
            }

            public ServiceAndBehaviorsContractResolver(IContractResolver serviceResolver)
            {
                this.serviceResolver = serviceResolver;
                behaviorContracts = new Dictionary<string, ContractDescription>();
            }

            public ContractDescription ResolveContract(string contractName)
            {
                ContractDescription contract = serviceResolver.ResolveContract(contractName);

                if (contract == null)
                {
                    contract = behaviorContracts.ContainsKey(contractName) ? behaviorContracts[contractName] : null;
                }

                return contract;
            }
        }
    }
}