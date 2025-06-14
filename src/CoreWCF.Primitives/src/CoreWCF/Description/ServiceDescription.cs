// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using CoreWCF.Collections.Generic;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Description
{
    public class ServiceDescription
    {
        private string _configurationName;
        private XmlName _serviceName;

        public ServiceDescription() { }

        public ServiceDescription(IEnumerable<ServiceEndpoint> endpoints) : this()
        {
            if (endpoints == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpoints));
            }

            foreach (ServiceEndpoint endpoint in endpoints)
            {
                Endpoints.Add(endpoint);
            }
        }

        public string Name
        {
            get
            {
                if (_serviceName != null)
                {
                    return _serviceName.EncodedName;
                }
                else if (ServiceType != null)
                {
                    return NamingHelper.XmlName(ServiceType.Name);
                }
                else
                {
                    return NamingHelper.DefaultServiceName;
                }
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _serviceName = null;
                }
                else
                {
                    // the XmlName ctor validate the value
                    _serviceName = new XmlName(value, true /*isEncoded*/);
                }
            }
        }

        public string Namespace { get; set; } = NamingHelper.DefaultNamespace;

        internal IServiceProvider ServiceProvider { get; set; }

        // This was KeyedByTypeCollection, maybe change to Collection<IServiceBehavior>
        public KeyedByTypeCollection<IServiceBehavior> Behaviors { get; } = new KeyedByTypeCollection<IServiceBehavior>();

        public string ConfigurationName
        {
            get { return _configurationName; }
            set
            {
                _configurationName = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        public ServiceEndpointCollection Endpoints { get; } = new ServiceEndpointCollection();

        public Type ServiceType { get; set; }

        internal static void AddBehaviors<TService>(ServiceDescription serviceDescription, IEnumerable<IServiceBehavior> injectedBehaviors = null) where TService : class
        {
            TypeLoader<TService>.ApplyServiceInheritance<IServiceBehavior, KeyedByTypeCollection<IServiceBehavior>>(
                serviceDescription.Behaviors, GetIServiceBehaviorAttributes);

            if (injectedBehaviors != null)
            {
                // Only add IServiceBehavior from DI if concrete type not already added
                // TODO: Add logging to state when an injected behavior has been skipped
                foreach(var behavior in injectedBehaviors)
                {
                    if (!serviceDescription.Behaviors.Contains(behavior.GetType()))
                    {
                        serviceDescription.Behaviors.Add(behavior);
                    }
                }
            }

            ServiceBehaviorAttribute serviceBehavior = EnsureBehaviorAttribute(serviceDescription);

            if (serviceBehavior.Name != null)
            {
                serviceDescription.Name = new XmlName(serviceBehavior.Name).EncodedName;
            }

            if (serviceBehavior.Namespace != null)
            {
                serviceDescription.Namespace = serviceBehavior.Namespace;
            }

            if (string.IsNullOrEmpty(serviceBehavior.ConfigurationName))
            {
                serviceDescription.ConfigurationName = typeof(TService).FullName;
            }
            else
            {
                serviceDescription.ConfigurationName = serviceBehavior.ConfigurationName;
            }
        }

        public static ServiceDescription GetService<TService>() where TService : class
        {
            // TODO: Make ServiceDescription generic?
            var description = new ServiceDescription
            {
                ServiceType = typeof(TService)
            };

            AddBehaviors<TService>(description);
            SetupSingleton(description, (TService)null);
            return description;
        }

        public static ServiceDescription GetService<TService>(TService serviceImplementation) where TService : class
        {
            if (serviceImplementation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serviceImplementation));
            }

            ServiceDescription description = new ServiceDescription
            {
                // TODO: What if the concrete type is different that the generic type?
                ServiceType = typeof(TService) //serviceImplementation.GetType();
            };

            if (serviceImplementation is IServiceBehavior behavior)
            {
                description.Behaviors.Add(behavior);
            }

            AddBehaviors<TService>(description);
            SetupSingleton(description, serviceImplementation);
            return description;
        }

        internal static TService CreateImplementation<TService>() where TService : class
        {
            if (!InvokerUtil.HasDefaultConstructor(typeof(TService)))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.SFxNoDefaultConstructorForType, typeof(TService).FullName)));
            }

            return (TService)InvokerUtil.GenerateCreateInstanceDelegate(typeof(TService))();
        }

        //internal static object CreateImplementation(Type serviceType)
        //{
        //    var constructors = serviceType.GetConstructors(TypeLoader.DefaultBindingFlags);
        //    ConstructorInfo constructor = null;
        //    foreach (var constr in constructors)
        //    {
        //        if (constr.GetParameters().Length == 0)
        //        {
        //            constructor = constr;
        //            break;
        //        }
        //    }

        //    if (constructor == null)
        //    {
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
        //            SR.SFxNoDefaultConstructor));
        //    }

        //    return constructor.Invoke(null, null);
        //}

        internal ServiceCredentials EnsureCredentials()
        {
            ServiceCredentials c = Behaviors.Find<ServiceCredentials>();

            if (c == null)
            {
                c = ServiceProvider?.GetRequiredService<ServiceCredentials>() ?? new ServiceCredentials();
                Behaviors.Add(c);
            }

            return c;
        }

        private static ServiceBehaviorAttribute EnsureBehaviorAttribute(ServiceDescription description)
        {
            ServiceBehaviorAttribute attr = description.Behaviors.Find<ServiceBehaviorAttribute>();

            if (attr == null)
            {
                attr = new ServiceBehaviorAttribute();
                description.Behaviors.Insert(0, attr);
            }

            return attr;
        }

        // This method ensures that the description object graph is structurally sound and that none
        // of the fundamental SFx framework assumptions have been violated.
        internal void EnsureInvariants()
        {
            for (int i = 0; i < Endpoints.Count; i++)
            {
                ServiceEndpoint endpoint = Endpoints[i];
                if (endpoint == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.AChannelServiceEndpointIsNull0));
                }
                endpoint.EnsureInvariants();
            }
        }

        private static void GetIServiceBehaviorAttributes(Type currentServiceType, KeyedByTypeCollection<IServiceBehavior> behaviors)
        {
            foreach (IServiceBehavior behaviorAttribute in ServiceReflector.GetCustomAttributes(currentServiceType, typeof(IServiceBehavior)))
            {
                behaviors.Add(behaviorAttribute);
            }
        }

        internal static void SetupSingleton<TService>(ServiceDescription serviceDescription, TService implementation) where TService : class
        {
            ServiceBehaviorAttribute serviceBehavior = EnsureBehaviorAttribute(serviceDescription);
            if (serviceBehavior.InstanceContextMode == InstanceContextMode.Single)
            {
                if (implementation == null)
                {
                    // implementation will only be null if not provided using DI
                    implementation = CreateImplementation<TService>();
                    serviceBehavior.SetHiddenSingleton(implementation);
                }
                else
                {
                    serviceBehavior.SetWellKnownSingleton(implementation);
                }
            }
        }

        internal static void SetupSingleton<TService>(ServiceDescription serviceDescription, IServiceProvider services) where TService : class
        {
            ServiceBehaviorAttribute serviceBehavior = serviceDescription.Behaviors.Find<ServiceBehaviorAttribute>();
            Debug.Assert(serviceBehavior != null, "EnsureServiceBehavior should have ensured the serviceBehavior");

            if (serviceBehavior.InstanceContextMode == InstanceContextMode.Single)
            {
                TService implementation = services.GetService<TService>();
                if (implementation == null)
                {
                    implementation = CreateImplementation<TService>();
                    serviceBehavior.SetHiddenSingleton(implementation);
                }
                else
                {
                    serviceBehavior.SetWellKnownSingleton(implementation);
                }
            }
        }

        private class ReflectedContractCollection : KeyedCollection<Type, ContractDescription>
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
    }

    internal class ServiceDescription<TService> : ServiceDescription where TService : class
    {
        public ServiceDescription(IEnumerable<IServiceBehavior> injectedBehaviors, IServiceProvider services)
        {
            ServiceType = typeof(TService);
            ServiceProvider = services;
            // Clone IServiceBehavior DI services implementing ICloneable to allow per service override.
            var behaviors = injectedBehaviors.ToList();

            if (services is IKeyedServiceProvider keyedServiceProvider)
            {
                behaviors.AddRange(keyedServiceProvider.GetKeyedServices<IServiceBehavior>(ServiceType));
            }

            for (int i = 0; i < behaviors.Count; i++)
            {
                if(behaviors[i] is ICloneable cloneable)
                {
                    behaviors[i] = (IServiceBehavior)cloneable.Clone();
                }
            }

            AddBehaviors<TService>(this, behaviors);
            SetupSingleton<TService>(this, services);
        }
    }
}
