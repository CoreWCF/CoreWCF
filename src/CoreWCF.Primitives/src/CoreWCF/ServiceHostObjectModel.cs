// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Collections.Generic;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreWCF
{
    internal class ServiceHostObjectModel<TService> : ServiceHostBase where TService : class
    {
        private IDisposable _disposableInstance;
        private readonly IServiceProvider _serviceProvider;
#pragma warning disable IDE0052 // Remove unread private members - see issue #286
        private readonly ILogger<ServiceHostObjectModel<TService>> _logger;
#pragma warning restore IDE0052 // Remove unread private members

        public ServiceHostObjectModel(IServiceProvider serviceProvider, ServiceBuilder serviceBuilder, ILogger<ServiceHostObjectModel<TService>> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            WaitForServiceBuilderOpening(serviceBuilder);

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
            IEnumerable<IServiceBehavior> serviceBehaviors = _serviceProvider.GetServices<IServiceBehavior>();
            foreach (IServiceBehavior behavior in serviceBehaviors)
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
                if (serviceInstanceUsedAsABehavior is IServiceBehavior behavior)
                {
                    description.Behaviors.Add(behavior);
                }
            }

            ReflectedContractCollection reflectedContracts = new ReflectedContractCollection();
            List<Type> interfaces = ServiceReflector.GetInterfaces<TService>();
            for (int i = 0; i < interfaces.Count; i++)
            {
                Type contractType = interfaces[i];
                if (!reflectedContracts.Contains(contractType))
                {
                    ContractDescription contract;
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

        internal class ReflectedContractCollection : KeyedCollection<Type, ContractDescription>
        {
            public ReflectedContractCollection()
                : base(null, 4)
            {
            }

            protected override Type GetKeyForItem(ContractDescription item)
            {
                if (item == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(item));
                }

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

        private class ReflectedAndBehaviorContractCollection
        {
            private readonly ReflectedContractCollection _reflectedContracts;
            private readonly KeyedByTypeCollection<IServiceBehavior> _behaviors;
            public ReflectedAndBehaviorContractCollection(ReflectedContractCollection reflectedContracts, KeyedByTypeCollection<IServiceBehavior> behaviors)
            {
                _reflectedContracts = reflectedContracts;
                _behaviors = behaviors;
            }

            internal bool Contains(Type implementedContract)
            {
                if (_reflectedContracts.Contains(implementedContract))
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
                if (_reflectedContracts.Contains(implementedContract))
                {
                    return ReflectedContractCollection.GetConfigKey(_reflectedContracts[implementedContract]);
                }

                //if (this.behaviors.Contains(typeof(ServiceMetadataBehavior)) && ServiceMetadataBehavior.IsMetadataImplementedType(implementedContract))
                //{
                //    return ServiceMetadataBehavior.MexContractName;
                //}

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SfxReflectedContractKeyNotFound2, implementedContract.FullName, string.Empty)));
            }
        }

        protected override void ApplyConfiguration()
        {
            base.ApplyConfiguration();
            IServer server = _serviceProvider.GetRequiredService<IServer>();
            IServerAddressesFeature addresses = server.Features.Get<IServerAddressesFeature>();
            foreach(string address in addresses.Addresses)
            {
                if (!address.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    // IIS hosting can populate Addresses with net.tcp/net.pipe/net.msmq addresses
                    // if the site has those bindings
                    continue;
                }

                var fixedUri = FixUri(address);
                // ASP.NET Core assumes all listeners are http. Other transports such as NetTcp will already be populated
                // in the base addresses so filter them out.
                bool skip = false;
                foreach(var baseAddress in InternalBaseAddresses)
                {
                    if(baseAddress.Port == fixedUri.Port) // Already added with a different protocol
                    {
                        skip = true;
                        break;
                    }
                }

                if (!skip)
                {
                    AddBaseAddress(FixUri(address));
                }
            }
        }

        private static Uri FixUri(string address)
        {
            if (address.StartsWith("http://+:", StringComparison.OrdinalIgnoreCase) ||
                address.StartsWith("http://+/", StringComparison.OrdinalIgnoreCase) ||
                address.StartsWith("http://*:", StringComparison.OrdinalIgnoreCase) ||
                address.StartsWith("http://*/", StringComparison.OrdinalIgnoreCase))
            {
                address = "http://localhost" + address.Substring(8);
            }
            else if (address.StartsWith("https://+:", StringComparison.OrdinalIgnoreCase) ||
                     address.StartsWith("https://+/", StringComparison.OrdinalIgnoreCase) ||
                     address.StartsWith("https://*:", StringComparison.OrdinalIgnoreCase) ||
                     address.StartsWith("https://*/", StringComparison.OrdinalIgnoreCase))
            {
                address = "https://localhost" + address.Substring(8);
            }

            return new Uri(address);
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
        private void WaitForServiceBuilderOpening(ServiceBuilder serviceBuilder)
        {
            serviceBuilder.WaitForOpening().GetAwaiter().GetResult();
            serviceBuilder.ThrowIfDisposedOrNotOpen();
        }
    }
}
