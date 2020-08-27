using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CoreWCF.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Description;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.Server;
using System.Threading.Tasks;
using System.Threading;
using CoreWCF.Configuration;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CoreWCF
{
    internal class ServiceHostObjectModel<TService> : ServiceHostBase where TService : class
    {
        private IDisposable _disposableInstance;
        private TService _singletonInstance;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ServiceHostObjectModel<TService>> _logger;
        private IServerAddressesFeature _serverAddresses;

        public ServiceHostObjectModel(IServiceProvider serviceProvider, IServer server, IServiceBuilder serviceBuilder, ILogger<ServiceHostObjectModel<TService>> logger)
        {
            _serviceProvider = serviceProvider;
            _serverAddresses = server.Features.Get<IServerAddressesFeature>();
            _logger = logger;

            InitializeBaseAddresses(serviceBuilder);

            InitializeDescription(new UriSchemeKeyedCollection(serviceBuilder.BaseAddresses.ToArray()));
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
            TService instance = _serviceProvider.GetService<TService>();
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
            object serviceInstanceUsedAsABehavior = serviceBehavior.GetWellKnownSingleton();
            if (serviceInstanceUsedAsABehavior == null)
            {
                serviceInstanceUsedAsABehavior = serviceBehavior.GetHiddenSingleton();
                _disposableInstance = serviceInstanceUsedAsABehavior as IDisposable;
            }

            if ((typeof(IServiceBehavior).IsAssignableFrom(typeof(TService)) || typeof(IContractBehavior).IsAssignableFrom(typeof(TService)))
                && serviceInstanceUsedAsABehavior == null)
            {
                if (instance == null)
                {
                    serviceInstanceUsedAsABehavior = ServiceDescription.CreateImplementation<TService>();
                }
                else
                {
                    serviceInstanceUsedAsABehavior = instance;
                }

                _disposableInstance = serviceInstanceUsedAsABehavior as IDisposable;
            }

            if (instance != null)
            {
                if (serviceBehavior.InstanceContextMode == InstanceContextMode.Single)
                {
                    SingletonInstance = instance;
                }
                else
                {
                    serviceBehavior.InstanceProvider = new DependencyInjectionInstanceProvider(_serviceProvider, typeof(TService));
                    if (serviceInstanceUsedAsABehavior == null && instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }

            if (instance == null)
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
                        contract = ContractDescription.GetContract<TService>(contractType, serviceInstanceUsedAsABehavior);
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
            EnsureBaseAddresses();
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
                    UriBuilder listenUriBuilder = new UriBuilder(binding.Scheme, DnsCache.MachineName);
                    result = new Uri(listenUriBuilder.Uri, uri);
                }
            }

            return result;
        }

        private void EnsureBaseAddresses()
        {
            if (_serverAddresses != null)
            {
                foreach(var addr in _serverAddresses.Addresses)
                {
                    var uri = new Uri(addr);
                    bool skip = false;
                    foreach(var baseAddress in InternalBaseAddresses)
                    {
                        if (baseAddress.Port == uri.Port && baseAddress.Scheme != uri.Scheme)
                        {
                            // ASP.NET Core adds net.tcp uri's as http{s} uri's
                            skip = true;
                            break;
                        }
                    }
                    if (!skip && !InternalBaseAddresses.Contains(uri))
                    {

                        InternalBaseAddresses.Add(uri);
                    }
                }

                if (_serverAddresses.Addresses.Count > 0)
                {
                    // It was populated by ASP.NET Core so can skip re-adding in future.
                    _serverAddresses = null;
                }
            }
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

        protected override void OnAbort() { }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Adds any implicitly-bound addresses to the service builder base addresses, so that net.tcp://
        /// bindings will correctly work on them. This is only necessary when the port provided to `UseNetTcp` is 0.
        ///
        /// <remarks>
        /// Currently, this does not remove the bound address from the server addresses. The framework removes
        /// the `net.tcp` scheme and replaces with with `http`, so the host name itself is checked.
        ///
        /// Another option that might be cleaner, would be to remove the use of `serviceBuilder.BaseAddresses` or
        /// populate it at runtime once from ServerAddresses, and disallow adding values by hand.
        /// </remarks>
        /// </summary>
        /// <param name="serviceBuilder"></param>
        private void InitializeBaseAddresses(IServiceBuilder serviceBuilder)
        {
            var addresses = _serverAddresses.Addresses.ToArray();
            for (var i = addresses.Length - 1; i >= 0; i--)
            {
                var address = addresses[i];
                var uri = new Uri(address, UriKind.Absolute);
                if (!uri.Host.Equals("0.0.0.0"))
                    continue;

                var baseAddress = new Uri($"net.tcp://{uri.Host}:{uri.Port}");
                _logger.LogDebug($"Adding base address {baseAddress} to serviceBuilder");
                serviceBuilder.BaseAddresses.Add(baseAddress);
            }
        }
    }
}
