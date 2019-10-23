using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CoreWCF.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Description;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF
{
    internal class ServiceHostObjectModel<TService> : ServiceHostBase where TService : class
    {
        private IDisposable _disposableInstance;
        private TService _singletonInstance;
        private readonly IServiceProvider _serviceProvider;

        public ServiceHostObjectModel(IServiceProvider serviceProvider, Uri[] baseAddresses)
        {
            _serviceProvider = serviceProvider;
            InitializeDescription(new UriSchemeKeyedCollection(baseAddresses));
        }

        public ServiceHostObjectModel(TService singletonInstance, Uri[] baseAddresses)
        {
            SingletonInstance = singletonInstance ?? throw new ArgumentNullException(nameof(singletonInstance));

            InitializeDescription(new UriSchemeKeyedCollection(baseAddresses));
        }

        public TService SingletonInstance { get; private set; }

        internal override object DisposableInstance
        {
            get
            {
                return _disposableInstance;
            }
        }

        internal ReflectedContractCollection ReflectedContracts { get; private set; }

        protected override ServiceDescription CreateDescription(out IDictionary<string, ContractDescription> implementedContracts)
        {
            ServiceDescription description;
            TService instance = _serviceProvider.GetService(typeof(TService)) as TService;
            if (instance != null)
            {
                description = ServiceDescription.GetService(instance);
            }
            else
            {
                description = ServiceDescription.GetService<TService>();
            }

            // Any user supplied IServiceBehaviors can be applied now
            var serviceBehaviors = _serviceProvider.GetServices<IServiceBehavior>();
            foreach (var behavior in serviceBehaviors)
            {
                description.Behaviors.Add(behavior);
            }

            ServiceBehaviorAttribute serviceBehavior = description.Behaviors.Find<ServiceBehaviorAttribute>();
            if (instance != null)
            {
                if (serviceBehavior.InstanceContextMode == InstanceContextMode.Single)
                {
                    SingletonInstance = instance;
                }
                else
                {
                    serviceBehavior.InstanceProvider = new DependencyInjectionInstanceProvider(_serviceProvider, typeof(TService));
                    if (instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }

            TService serviceInstanceUsedAsABehavior = (TService)serviceBehavior.GetWellKnownSingleton();
            if (serviceInstanceUsedAsABehavior == null)
            {
                serviceInstanceUsedAsABehavior = (TService)serviceBehavior.GetHiddenSingleton();
                _disposableInstance = serviceInstanceUsedAsABehavior as IDisposable;
            }

            if ((typeof(IServiceBehavior).IsAssignableFrom(typeof(TService)) || typeof(IContractBehavior).IsAssignableFrom(typeof(TService)))
                && serviceInstanceUsedAsABehavior == null)
            {
                serviceInstanceUsedAsABehavior = ServiceDescription.CreateImplementation<TService>();
                _disposableInstance = serviceInstanceUsedAsABehavior as IDisposable;
            }

            if (SingletonInstance == null)
            {
                if (serviceInstanceUsedAsABehavior is IServiceBehavior)
                {
                    description.Behaviors.Add((IServiceBehavior)serviceInstanceUsedAsABehavior);
                }
            }

            ReflectedContractCollection reflectedContracts = new ReflectedContractCollection();
            List<Type> interfaces = ServiceReflector.GetInterfaces<TService>();
            for (int i = 0; i < interfaces.Count; i++)
            {
                Type contractType = interfaces[i];
                if (!reflectedContracts.Contains(contractType))
                {
                    ContractDescription contract = null;
                    if (serviceInstanceUsedAsABehavior != null)
                    {
                        contract = ContractDescription.GetContract(contractType, serviceInstanceUsedAsABehavior);
                    }
                    else
                    {
                        contract = ContractDescription.GetContract<TService>(contractType);
                    }

                    reflectedContracts.Add(contract);
                    Collection<ContractDescription> inheritedContracts = contract.GetInheritedContracts();
                    for (int j = 0; j < inheritedContracts.Count; j++)
                    {
                        ContractDescription inheritedContract = inheritedContracts[j];
                        if (!reflectedContracts.Contains(inheritedContract.ContractType))
                        {
                            reflectedContracts.Add(inheritedContract);
                        }
                    }
                }
            }
            ReflectedContracts = reflectedContracts;

            implementedContracts = reflectedContracts.ToImplementedContracts();
            return description;
        }

        protected override void ApplyConfiguration()
        {
            // Prevent base class throw by overriding
        }

        internal class ReflectedContractCollection : KeyedCollection<Type, ContractDescription>
        {
            public ReflectedContractCollection()
                : base(null, 4)
            {
            }

            protected override Type GetKeyForItem(ContractDescription item)
            {
                if (item == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));

                return item.ContractType;
            }

            public IDictionary<string, ContractDescription> ToImplementedContracts()
            {
                Dictionary<string, ContractDescription> implementedContracts = new Dictionary<string, ContractDescription>();
                foreach (ContractDescription contract in Items)
                {
                    implementedContracts.Add(GetConfigKey(contract), contract);
                }
                return implementedContracts;
            }

            internal static string GetConfigKey(ContractDescription contract)
            {
                return contract.ConfigurationName;
            }
        }

        class ReflectedAndBehaviorContractCollection
        {
            ReflectedContractCollection reflectedContracts;
            KeyedByTypeCollection<IServiceBehavior> behaviors;
            public ReflectedAndBehaviorContractCollection(ReflectedContractCollection reflectedContracts, KeyedByTypeCollection<IServiceBehavior> behaviors)
            {
                this.reflectedContracts = reflectedContracts;
                this.behaviors = behaviors;
            }

            internal bool Contains(Type implementedContract)
            {
                if (this.reflectedContracts.Contains(implementedContract))
                {
                    return true;
                }

                //if (this.behaviors.Contains(typeof(ServiceMetadataBehavior)) && ServiceMetadataBehavior.IsMetadataImplementedType(implementedContract))
                //{
                //    return true;
                //}

                return false;
            }

            internal string GetConfigKey(Type implementedContract)
            {
                if (reflectedContracts.Contains(implementedContract))
                {
                    return ReflectedContractCollection.GetConfigKey(reflectedContracts[implementedContract]);
                }

                //if (this.behaviors.Contains(typeof(ServiceMetadataBehavior)) && ServiceMetadataBehavior.IsMetadataImplementedType(implementedContract))
                //{
                //    return ServiceMetadataBehavior.MexContractName;
                //}

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SfxReflectedContractKeyNotFound2, implementedContract.FullName, string.Empty)));

            }
        }

        internal Uri MakeAbsoluteUri(Uri uri, Binding binding)
        {
            Uri result = uri;
            if (!result.IsAbsoluteUri)
            {
                if (binding.Scheme == string.Empty)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SFxCustomBindingWithoutTransport));
                }
                result = GetVia(binding.Scheme, result, InternalBaseAddresses);
                if (result == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxEndpointNoMatchingScheme, binding.Scheme, binding.Name, GetBaseAddressSchemes(InternalBaseAddresses))));
                }
            }
            return result;
        }

        internal static String GetBaseAddressSchemes(UriSchemeKeyedCollection uriSchemeKeyedCollection)
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
                if (!baseAddresses.Contains(scheme))
                {
                    return null;
                }

                via = GetUri(baseAddresses[scheme], address);
            }
            return via;
        }

        internal static Uri GetUri(Uri baseUri, Uri relativeUri)
        {
            var path = relativeUri.OriginalString;
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
                return baseUri;

            if (!baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
            {
                baseUri = new Uri(baseUri.AbsoluteUri + "/");
            }

            return new Uri(baseUri, path);
        }

    }
}
