// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using CoreWCF.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    internal class ConfigurationHolder : IConfigurationHolder
    {
        private readonly ConcurrentDictionary<string, Binding> _bindings = new ConcurrentDictionary<string, Binding>();
        private readonly ConcurrentDictionary<string, ServiceEndpoint> _endpoints = new ConcurrentDictionary<string, ServiceEndpoint>();
        private readonly IServiceProvider _provider;
        private readonly IBindingFactory _factoryBinding;

        public ConcurrentDictionary<string, ServiceEndpoint> Endpoints => _endpoints;

        public ConfigurationHolder(IServiceProvider serviceProvider, IBindingFactory factory)
        {
            _provider = serviceProvider;
            _factoryBinding = factory;
        }

        public void AddBinding(Binding binding)
        {
            _bindings.TryAdd(binding.Name, binding);
        }

        public Binding ResolveBinding(string bindingType, string name)
        {
            if (string.IsNullOrEmpty(bindingType))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(bindingType);
            }

            if (string.IsNullOrEmpty(name))
            {
                return _factoryBinding.Create(bindingType);
            }

            if (_bindings.TryGetValue(name, out Binding binding))
            {
                return binding;
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new BindingNotFoundException());
        }

        public IXmlConfigEndpoint GetXmlConfigEndpoint(string name)
        {
            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(name);
            }

            if (_endpoints.TryGetValue(name, out ServiceEndpoint endpoint))
            {
                Type contract = ServiceReflector.ResolveTypeFromName(endpoint.Contract);
                Type service = ServiceReflector.ResolveTypeFromName(endpoint.ServiceName);
                Binding binding = ResolveBinding(endpoint.Binding, endpoint.BindingConfiguration);

                return new XmlConfigEndpoint(service, contract, binding, endpoint.Address);
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new EndpointNotFoundException($"Endpoint {name} not found in xml configuration"));

        }

        public void AddServiceEndpoint(string name, string serviceName, Uri address, string contract, string bindingType, string bindingName)
        {
            var endpoint = new ServiceEndpoint
            {
                ServiceName = serviceName,
                Address = address,
                Binding = bindingType,
                Contract = contract,
                Name = name,
                BindingConfiguration = bindingName
            };

            _endpoints.TryAdd(name, endpoint);
        }
    }
}
