using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Collections.Generic;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Channels;
using Microsoft.ServiceModel.Description;
using Microsoft.ServiceModel.Diagnostics;
using Microsoft.ServiceModel.Dispatcher;
using System.Diagnostics;

namespace Microsoft.ServiceModel
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
        //ServiceAuthorizationBehavior readOnlyAuthorization;
        //ServiceAuthenticationBehavior readOnlyAuthentication;
        Dictionary<DispatcherBuilder.ListenUriInfo, Collection<ServiceEndpoint>> endpointsByListenUriInfo;
        int busyCount;
        //EventTraceActivity eventTraceActivity;

        public event EventHandler<UnknownMessageReceivedEventArgs> UnknownMessageReceived;

        protected ServiceHostBase()
        {
            this.baseAddresses = new UriSchemeKeyedCollection(this.ThisLock);
            this.channelDispatchers = new ChannelDispatcherCollection(this, this.ThisLock);
            this.extensions = new ExtensionCollection<ServiceHostBase>(this, this.ThisLock);
            this.instances = new InstanceContextManager(this.ThisLock);
        }

        // TODO: Bring in ServiceAuthorizationBehavior
        //public ServiceAuthorizationBehavior Authorization
        //{
        //    get
        //    {
        //        if (this.Description == null)
        //        {
        //            return null;
        //        }
        //        else if (this.State == CommunicationState.Created || this.State == CommunicationState.Opening)
        //        {
        //            return EnsureAuthorization(this.Description);
        //        }
        //        else
        //        {
        //            return this.readOnlyAuthorization;
        //        }
        //    }
        //}

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
                externalBaseAddresses = new ReadOnlyCollection<Uri>(new List<Uri>(this.baseAddresses));
                return externalBaseAddresses;
            }
        }

        public ChannelDispatcherCollection ChannelDispatchers
        {
            get { return this.channelDispatchers; }
        }

        public TimeSpan CloseTimeout
        {
            get { return this.closeTimeout; }
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

                lock (this.ThisLock)
                {
                    this.ThrowIfClosedOrOpened();
                    this.closeTimeout = value;
                }
            }
        }

        public ServiceCredentials Credentials
        {
            get
            {
                // TODO: Decide if Credentials should be populated?
                return null;
                //if (this.Description == null)
                //{
                //    return null;
                //}
                //else if (this.State == CommunicationState.Created || this.State == CommunicationState.Opening)
                //{
                //    return EnsureCredentials(this.Description);
                //}
                //else
                //{
                //    return this.readOnlyCredentials;
                //}
            }
        }

        protected override TimeSpan DefaultCloseTimeout
        {
            get { return this.CloseTimeout; }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get { return this.OpenTimeout; }
        }

        public ServiceDescription Description
        {
            get { return this.description; }
        }

        public IExtensionCollection<ServiceHostBase> Extensions
        {
            get { return this.extensions; }
        }

        protected internal IDictionary<string, ContractDescription> ImplementedContracts
        {
            get { return this.implementedContracts; }
        }

        internal UriSchemeKeyedCollection InternalBaseAddresses
        {
            get { return this.baseAddresses; }
        }

        public int ManualFlowControlLimit
        {
            get { return 0; /*return this.ServiceThrottle.ManualFlowControlLimit;*/ }
            set { /*this.ServiceThrottle.ManualFlowControlLimit = value;*/ }
        }

        public TimeSpan OpenTimeout
        {
            get { return this.openTimeout; }
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

                lock (this.ThisLock)
                {
                    this.ThrowIfClosedOrOpened();
                    this.openTimeout = value;
                }
            }
        }

        protected void AddBaseAddress(Uri baseAddress)
        {
            if (this.initializeDescriptionHasFinished)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.SFxCannotCallAddBaseAddress));
            }
            this.baseAddresses.Add(baseAddress);
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

        void IDisposable.Dispose()
        {
            CloseAsync().GetAwaiter().GetResult();
        }

        protected abstract ServiceDescription CreateDescription(out IDictionary<string, ContractDescription> implementedContracts);

        protected virtual void InitializeRuntime()
        {
            throw new PlatformNotSupportedException();
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
            IDictionary<string, ContractDescription> implementedContracts = null;
            ServiceDescription description = CreateDescription(out implementedContracts);
            this.description = description;
            this.implementedContracts = implementedContracts;

            ApplyConfiguration();
            this.initializeDescriptionHasFinished = true;
        }

        // Configuration
        //protected void LoadConfigurationSection(ServiceElement serviceSection)
        //{
        //    throw new PlatformNotSupportedException();
        //}

        protected override void OnAbort()
        {
            throw new PlatformNotSupportedException();
        }

        protected override Task OnCloseAsync(CancellationToken cancellationToken)
        {
            throw new PlatformNotSupportedException();
        }



        protected override Task OnOpenAsync(CancellationToken cancellationToken)
        {
            throw new PlatformNotSupportedException();
        }

        protected override void OnClosed()
        {
            throw new PlatformNotSupportedException();
        }

        protected override void OnOpened()
        {
            throw new PlatformNotSupportedException();
        }
        
        protected void ReleasePerformanceCounters()
        {
            throw new PlatformNotSupportedException();
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
                return this.implementedContracts != null && this.implementedContracts.ContainsKey(contractName) ? this.implementedContracts[contractName] : null;
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
                    contract = this.behaviorContracts.ContainsKey(contractName) ? this.behaviorContracts[contractName] : null;
                }

                return contract;
            }
        }
    }
}