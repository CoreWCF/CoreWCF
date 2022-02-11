// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF
{
    public abstract class ServiceHostBase : CommunicationObject, IExtensibleObject<ServiceHostBase>, IDisposable
    {
        internal static readonly Uri s_emptyUri = new Uri(string.Empty, UriKind.RelativeOrAbsolute);
        private bool _initializeDescriptionHasFinished;
        private TimeSpan _closeTimeout = ServiceDefaults.ServiceHostCloseTimeout;
        private readonly ExtensionCollection<ServiceHostBase> _extensions;
        private ReadOnlyCollection<Uri> _externalBaseAddresses;
        private IDictionary<string, ContractDescription> _implementedContracts;
        private readonly IInstanceContextManager _instances;
        private TimeSpan _openTimeout = ServiceDefaults.OpenTimeout;

        //ServiceAuthenticationBehavior readOnlyAuthentication;
        //EventTraceActivity eventTraceActivity;

#pragma warning disable CS0067 // The event is never used - see issue #288
        public event EventHandler<UnknownMessageReceivedEventArgs> UnknownMessageReceived;
#pragma warning restore CS0067 // The event is never used

        protected ServiceHostBase()
        {
            InternalBaseAddresses = new UriSchemeKeyedCollection(ThisLock);
            ChannelDispatchers = new ChannelDispatcherCollection(this, ThisLock);
            _extensions = new ExtensionCollection<ServiceHostBase>(this, ThisLock);
            _instances = new InstanceContextManager(ThisLock);
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
                    return null;
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
                _externalBaseAddresses = new ReadOnlyCollection<Uri>(new List<Uri>(InternalBaseAddresses));
                return _externalBaseAddresses;
            }
        }

        public ChannelDispatcherCollection ChannelDispatchers { get; }

        public TimeSpan CloseTimeout
        {
            get { return _closeTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    string message = SR.SFxTimeoutOutOfRange0;
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), message));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.SFxTimeoutOutOfRangeTooBig));
                }

                lock (ThisLock)
                {
                    ThrowIfClosedOrOpened();
                    _closeTimeout = value;
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
                    return null;
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

        public ServiceDescription Description { get; private set; }

        public IExtensionCollection<ServiceHostBase> Extensions
        {
            get { return _extensions; }
        }

        protected internal IDictionary<string, ContractDescription> ImplementedContracts
        {
            get { return _implementedContracts; }
        }

        internal UriSchemeKeyedCollection InternalBaseAddresses { get; }

        public int ManualFlowControlLimit
        {
            get { return 0; /*return this.ServiceThrottle.ManualFlowControlLimit;*/ }
            set { /*this.ServiceThrottle.ManualFlowControlLimit = value;*/ }
        }

        public TimeSpan OpenTimeout
        {
            get { return _openTimeout; }
            set
            {
                if (value < TimeSpan.Zero)
                {
                    string message = SR.SFxTimeoutOutOfRange0;
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), message));
                }
                if (TimeoutHelper.IsTooLarge(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), SR.SFxTimeoutOutOfRangeTooBig));
                }

                lock (ThisLock)
                {
                    ThrowIfClosedOrOpened();
                    _openTimeout = value;
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
            if (_initializeDescriptionHasFinished)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.SFxCannotCallAddBaseAddress));
            }

            InternalBaseAddresses.Add(baseAddress);
        }

        internal Uri MakeAbsoluteUri(Uri relativeOrAbsoluteUri, Binding binding)
        {
            return MakeAbsoluteUri(relativeOrAbsoluteUri, binding, InternalBaseAddresses);
        }

        internal static Uri MakeAbsoluteUri(Uri relativeOrAbsoluteUri, Binding binding, UriSchemeKeyedCollection baseAddresses)
        {
            Uri result = relativeOrAbsoluteUri;
            if (!result.IsAbsoluteUri)
            {
                if (binding.Scheme == string.Empty)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxCustomBindingWithoutTransport));
                }
                result = GetVia(binding.Scheme, result, baseAddresses);
                if (result == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxEndpointNoMatchingScheme, binding.Scheme, binding.Name, GetBaseAddressSchemes(baseAddresses))));
                }
            }

            return result;
        }

        protected virtual void ApplyConfiguration()
        {
            if (Description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxServiceHostBaseCannotApplyConfigurationWithoutDescription));
            }

            EnsureAuthenticationAuthorizationDebug(Description);
        }

        internal void EnsureAuthenticationAuthorizationDebug(ServiceDescription description)
        {
            //EnsureAuthentication(description);
            EnsureAuthorization(description);
            EnsureDebug(description);
        }

        internal virtual void BindInstance(InstanceContext instance)
        {
            _instances.Add(instance);
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

        private ServiceAuthorizationBehavior EnsureAuthorization(ServiceDescription description)
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

        private ServiceDebugBehavior EnsureDebug(ServiceDescription description)
        {
            Fx.Assert(State == CommunicationState.Created || State == CommunicationState.Opening, "");
            ServiceDebugBehavior m = description.Behaviors.Find<ServiceDebugBehavior>();

            if (m == null)
            {
                m = new ServiceDebugBehavior();
                description.Behaviors.Add(m);
            }

            return m;
        }

        private ServiceCredentials EnsureCredentials(ServiceDescription description)
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

        internal static string GetBaseAddressSchemes(UriSchemeKeyedCollection uriSchemeKeyedCollection)
        {
            StringBuilder buffer = new StringBuilder();
            bool firstScheme = true;
            foreach (Uri address in uriSchemeKeyedCollection)
            {
                if (firstScheme)
                {
                    buffer.Append(address.Scheme);
                    firstScheme = false;
                }
                else
                {
                    buffer.Append(CultureInfo.CurrentCulture.TextInfo.ListSeparator).Append(address.Scheme);
                }
            }

            return buffer.ToString();
        }

        internal static Uri GetVia(string scheme, Uri address, UriSchemeKeyedCollection baseAddresses)
        {
            Uri via = address;
            if (!via.IsAbsoluteUri)
            {
                Uri baseAddress = null;
                foreach(var ba in baseAddresses)
                {
                    if (ba.Scheme.Equals(scheme))
                    {
                        baseAddress = ba;
                        break;
                    }
                }
                if (baseAddress == null)
                {
                    return null;
                }

                via = GetUri(baseAddress, address);
            }
            return via;
        }

        internal Uri GetVia(string scheme, Uri address)
        {
            if (!address.IsAbsoluteUri)
            {
                Uri baseAddress = null;
                foreach (var ba in BaseAddresses)
                {
                    if (ba.Scheme.Equals(scheme))
                    {
                        baseAddress = ba;
                        break;
                    }
                }

                if (baseAddress == null)
                {
                    return null;
                }

                return GetUri(baseAddress, address.OriginalString);
            }

            return address;
        }

        private static Uri GetUri(Uri baseUri, string path)
        {
            if (path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("\\", StringComparison.Ordinal))
            {
                int i = 1;
                for (; i < path.Length; ++i)
                {
                    if (path[i] != '/' && path[i] != '\\')
                    {
                        break;
                    }
                }
                path = path.Substring(i);
            }

            if (path.Length == 0)
                return baseUri;

            if (!baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
            {
                baseUri = new Uri(baseUri.AbsoluteUri + "/");
            }
            return new Uri(baseUri, path);
        }

        internal static Uri GetUri(Uri baseUri, Uri relativeUri)
        {
            string path = relativeUri.OriginalString;
            if (path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("\\", StringComparison.Ordinal))
            {
                int i = 1;
                for (; i < path.Length; ++i)
                {
                    if (path[i] != '/' && path[i] != '\\')
                    {
                        break;
                    }
                }
                path = path.Substring(i);
            }

            // new Uri(Uri, string.Empty) is broken
            if (path.Length == 0)
            {
                return baseUri;
            }

            if (!baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
            {
                baseUri = new Uri(baseUri.AbsoluteUri + "/");
            }

            return new Uri(baseUri, path);
        }

        //public int IncrementManualFlowControlLimit(int incrementBy)
        //{
        //    throw new PlatformNotSupportedException();
        //}

        protected void InitializeDescription(UriSchemeKeyedCollection baseAddresses)
        {
            foreach (Uri baseAddress in baseAddresses)
            {
                InternalBaseAddresses.Add(baseAddress);
            }

            Description = CreateDescription(out _implementedContracts);
            ApplyConfiguration();
            _initializeDescriptionHasFinished = true;
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

        private void OnChannelDispatcherFaulted(object sender, EventArgs e)
        {
            Fault();
        }

        //protected void ReleasePerformanceCounters()
        //{
        //    throw new PlatformNotSupportedException();
        //}

        internal virtual void UnbindInstance(InstanceContext instance)
        {
            _instances.Remove(instance);
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

        private class ImplementedContractsContractResolver : IContractResolver
        {
            private readonly IDictionary<string, ContractDescription> _implementedContracts;

            public ImplementedContractsContractResolver(IDictionary<string, ContractDescription> implementedContracts)
            {
                _implementedContracts = implementedContracts;
            }

            public ContractDescription ResolveContract(string contractName)
            {
                return _implementedContracts != null && _implementedContracts.ContainsKey(contractName) ? _implementedContracts[contractName] : null;
            }
        }

        internal class ServiceAndBehaviorsContractResolver : IContractResolver
        {
            private readonly IContractResolver _serviceResolver;

            public Dictionary<string, ContractDescription> BehaviorContracts { get; }

            public ServiceAndBehaviorsContractResolver(IContractResolver serviceResolver)
            {
                _serviceResolver = serviceResolver;
                BehaviorContracts = new Dictionary<string, ContractDescription>();
            }

            public ContractDescription ResolveContract(string contractName)
            {
                ContractDescription contract = _serviceResolver.ResolveContract(contractName);

                if (contract == null)
                {
                    contract = BehaviorContracts.ContainsKey(contractName) ? BehaviorContracts[contractName] : null;
                }

                return contract;
            }
        }
    }
}
