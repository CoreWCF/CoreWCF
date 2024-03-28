// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    internal class ConfigurationHolder : IConfigurationHolder
    {
        private readonly IDictionary<string, Binding> _bindings = new Dictionary<string, Binding>();
        private readonly ISet<ServiceEndpoint> _endpoints = new HashSet<ServiceEndpoint>();
        private readonly IServiceProvider _provider;
        private readonly IBindingFactory _factoryBinding;

        public ISet<ServiceEndpoint> Endpoints => _endpoints;

        public ConfigurationHolder(IServiceProvider serviceProvider, IBindingFactory factory)
        {
            _provider = serviceProvider;
            _factoryBinding = factory;
        }

        public void AddBinding(Binding binding)
        {
            _bindings.Add(binding.Name, binding);
        }

        public Binding ResolveBinding(string bindingType, string name, string bindingNamespace = null)
        {
            if (string.IsNullOrEmpty(bindingType))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(bindingType);
            }

            Binding binding;
            if (string.IsNullOrEmpty(name))
            {
                binding = _factoryBinding.Create(bindingType);
            }
            else if (!_bindings.TryGetValue(name, out binding))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new BindingNotFoundException());
            }

            SetBindingNamespace(bindingNamespace, binding);

            return binding;

        }

        public IXmlConfigEndpoint GetXmlConfigEndpoint(ServiceEndpoint endpoint)
        {
            if (endpoint == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpoint));
            }

            Type contract = ServiceReflector.ResolveTypeFromName(endpoint.Contract);
            Type service = ServiceReflector.ResolveTypeFromName(endpoint.ServiceName);
            Binding binding = ResolveBinding(endpoint.Binding, endpoint.BindingConfiguration, endpoint.BindingNamespace);
            return new XmlConfigEndpoint(service, contract, binding, endpoint.Address);
        }

        public void AddServiceEndpoint(string name, string serviceName, Uri address, string contract, string bindingType, string bindingName, string bindingNamespace)
        {
            var endpoint = new ServiceEndpoint
            {
                ServiceName = serviceName,
                Address = address,
                Binding = bindingType,
                Contract = contract,
                Name = name,
                BindingConfiguration = bindingName,
                BindingNamespace = bindingNamespace
            };

            _endpoints.Add(endpoint);
        }

        private static void SetBindingNamespace(string bindingNamespace, Binding binding)
        {
            if (binding != null && bindingNamespace != null)
            {
                binding.Namespace = bindingNamespace;
            }
        }
    }
}
