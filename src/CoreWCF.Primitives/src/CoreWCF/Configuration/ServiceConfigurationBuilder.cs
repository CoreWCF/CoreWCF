// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public class ServiceConfigurationBuilder
    {
        private readonly List<Action<ServiceConfigurationBuilder>> _configDelegates = new List<Action<ServiceConfigurationBuilder>>();
        private Dictionary<Type, List<ServiceEndpointConfiguration>> _endpoints;

        internal Type ServiceType { get; set; }

        public ServiceConfigurationBuilder(Type serviceType)
        {
            ServiceType = serviceType;
        }

        public void AddConfigureDelegate(Action<ServiceConfigurationBuilder> configDelegate)
        {
            _configDelegates.Add(configDelegate);
        }

        public void ConfigureServiceEndpoint(Type service, Type implementedContract, Binding binding, Uri address, Uri listenUri)
        {
            List<ServiceEndpointConfiguration> serviceEndpoints;
            if (!_endpoints.TryGetValue(service, out serviceEndpoints))
            {
                serviceEndpoints = new List<ServiceEndpointConfiguration>();
                _endpoints[service] = serviceEndpoints;
            }

            var endpoint = new ServiceEndpointConfiguration
            {
                Contract = implementedContract,
                Binding = binding,
                Address = address,
                ListenUri = listenUri
            };
            serviceEndpoints.Add(endpoint);
        }

        internal void ConfigureServiceBuilder(IServiceBuilder serviceBuilder)
        {
            _endpoints = new Dictionary<Type, List<ServiceEndpointConfiguration>>();
            foreach (var configDelegate in _configDelegates)
            {
                configDelegate(this);
            }

            foreach (var service in _endpoints)
            {
                serviceBuilder.AddService(service.Key);
                foreach (var serviceEndpoint in service.Value)
                {
                    serviceBuilder.AddServiceEndpoint(service.Key, serviceEndpoint.Contract, serviceEndpoint.Binding, serviceEndpoint.Address, serviceEndpoint.ListenUri);
                }

            }
            _endpoints = null;
        }
    }
}
