using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;

namespace CoreWCF
{
    [AttributeUsage(CoreWCFAttributeTargets.ServiceBehavior)]
    public sealed class ServiceBehaviorAttribute : Attribute, IServiceBehavior
    {
        private ConcurrencyMode _concurrencyMode;
        bool _ensureOrderedDispatch = false;
        private string _configurationName;
        bool _includeExceptionDetailInFaults = false;
        private InstanceContextMode _instanceMode;
        private object _wellKnownSingleton;  // if the user passes an object to the ServiceHost, it is stored here
        private object _hiddenSingleton;     // if the user passes a type to the ServiceHost, and instanceMode==Single, we store the instance here
        bool _validateMustUnderstand = true;
        bool _ignoreExtensionDataObject = DataContractSerializerDefaults.IgnoreExtensionDataObject;
        int _maxItemsInObjectGraph = DataContractSerializerDefaults.MaxItemsInObjectGraph;
        bool _automaticSessionShutdown = true;
        IInstanceProvider _instanceProvider = null;
        bool _useSynchronizationContext = true;
        AddressFilterMode _addressFilterMode = AddressFilterMode.Exact;

        [DefaultValue(null)]
        public string Name { get; set; }

        [DefaultValue(null)]
        public string Namespace { get; set; }

        internal IInstanceProvider InstanceProvider
        {
            set { _instanceProvider = value; }
        }

        [DefaultValue(AddressFilterMode.Exact)]
        public AddressFilterMode AddressFilterMode
        {
            get { return _addressFilterMode; }
            set
            {
                if (!AddressFilterModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _addressFilterMode = value;
            }
        }

        [DefaultValue(null)]
        public string ConfigurationName
        {
            get { return _configurationName; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                if (value == string.Empty)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value),
                        SR.SFxConfigurationNameCannotBeEmpty));
                }

                _configurationName = value;
            }
        }

        [DefaultValue(ConcurrencyMode.Single)]
        public ConcurrencyMode ConcurrencyMode
        {
            get { return _concurrencyMode; }
            set
            {
                if (!ConcurrencyModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _concurrencyMode = value;
            }
        }

        [DefaultValue(InstanceContextMode.PerSession)]
        public InstanceContextMode InstanceContextMode
        {
            get { return _instanceMode; }
            set
            {
                if (!InstanceContextModeHelper.IsDefined(value))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value)));
                }

                _instanceMode = value;
            }
        }

        public object GetWellKnownSingleton()
        {
            return _wellKnownSingleton;
        }

        internal void SetWellKnownSingleton(object value)
        {
            if (value == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));

            _wellKnownSingleton = value;
        }

        internal object GetHiddenSingleton()
        {
            return _hiddenSingleton;
        }

        internal void SetHiddenSingleton(object value)
        {
            if (value == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));

            _hiddenSingleton = value;
        }

        void IServiceBehavior.Validate(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            if (_concurrencyMode != ConcurrencyMode.Single && _ensureOrderedDispatch)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxNonConcurrentOrEnsureOrderedDispatch, description.Name)));
            }
        }

        void IServiceBehavior.AddBindingParameters(ServiceDescription serviceDescription,
            ServiceHostBase serviceHostBase,
            Collection<ServiceEndpoint> endpoints,
            BindingParameterCollection bindingParameters)
        {
        }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            for (int i = 0; i < serviceHostBase.ChannelDispatchers.Count; i++)
            {
                ChannelDispatcher channelDispatcher = serviceHostBase.ChannelDispatchers[i] as ChannelDispatcher;
                if (channelDispatcher != null)
                {
                    channelDispatcher.IncludeExceptionDetailInFaults = _includeExceptionDetailInFaults;

                    if (channelDispatcher.HasApplicationEndpoints)
                    {
                        foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
                        {
                            if (endpointDispatcher.IsSystemEndpoint)
                            {
                                continue;
                            }
                            DispatchRuntime behavior = endpointDispatcher.DispatchRuntime;
                            behavior.ConcurrencyMode = _concurrencyMode;
                            behavior.EnsureOrderedDispatch = _ensureOrderedDispatch;
                            behavior.ValidateMustUnderstand = _validateMustUnderstand;
                            behavior.AutomaticInputSessionShutdown = _automaticSessionShutdown;
                            if (!_useSynchronizationContext)
                            {
                                behavior.SynchronizationContext = null;
                            }

                            if (!endpointDispatcher.AddressFilterSetExplicit)
                            {
                                EndpointAddress address = endpointDispatcher.OriginalAddress;
                                if (address == null || AddressFilterMode == AddressFilterMode.Any)
                                {
                                    endpointDispatcher.AddressFilter = new MatchAllMessageFilter();
                                }
                                else if (AddressFilterMode == AddressFilterMode.Prefix)
                                {
                                    endpointDispatcher.AddressFilter = new PrefixEndpointAddressMessageFilter(address);
                                }
                                else if (AddressFilterMode == AddressFilterMode.Exact)
                                {
                                    endpointDispatcher.AddressFilter = new EndpointAddressMessageFilter(address);
                                }
                            }
                        }
                    }
                }
            }
            DataContractSerializerServiceBehavior.ApplySerializationSettings(description, _ignoreExtensionDataObject, _maxItemsInObjectGraph);
            ApplyInstancing(description, serviceHostBase);
        }

        void ApplyInstancing(ServiceDescription description, ServiceHostBase serviceHostBase)
        {
            Type serviceType = description.ServiceType;
            InstanceContext singleton = null;

            for (int i = 0; i < serviceHostBase.ChannelDispatchers.Count; i++)
            {
                ChannelDispatcher channelDispatcher = serviceHostBase.ChannelDispatchers[i] as ChannelDispatcher;
                if (channelDispatcher != null)
                {
                    foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
                    {
                        if (endpointDispatcher.IsSystemEndpoint)
                        {
                            continue;
                        }
                        DispatchRuntime dispatch = endpointDispatcher.DispatchRuntime;
                        if (dispatch.InstanceProvider == null)
                        {
                            if (_instanceProvider == null)
                            {
                                if (serviceType == null && _wellKnownSingleton == null)
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.InstanceSettingsMustHaveTypeOrWellKnownObject0));

                                if (_instanceMode != InstanceContextMode.Single && _wellKnownSingleton != null)
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxWellKnownNonSingleton0));
                            }
                            else
                            {
                                dispatch.InstanceProvider = _instanceProvider;
                            }
                        }
                        dispatch.Type = serviceType;
                        dispatch.InstanceContextProvider = InstanceContextProviderBase.GetProviderForMode(_instanceMode, dispatch);

                        if ((_instanceMode == InstanceContextMode.Single) &&
                            (dispatch.SingletonInstanceContext == null))
                        {
                            if (singleton == null)
                            {
                                if (_wellKnownSingleton != null)
                                {
                                    singleton = new InstanceContext(serviceHostBase, _wellKnownSingleton, true, false);
                                }
                                else if (_hiddenSingleton != null)
                                {
                                    singleton = new InstanceContext(serviceHostBase, _hiddenSingleton, false, false);
                                }
                                else
                                {
                                    singleton = new InstanceContext(serviceHostBase, false);
                                }

                                singleton.AutoClose = false;
                            }
                            dispatch.SingletonInstanceContext = singleton;
                        }
                    }
                }
            }
        }

    }
}