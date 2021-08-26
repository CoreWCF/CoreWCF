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
        private List<ServiceEndpointConfiguration> _endpoints = new List<ServiceEndpointConfiguration>();
        private Type _serviceType;

        public ServiceConfigurationBuilder(Type serviceType)
        {
            _serviceType = serviceType;
        }

        public void Configure(Action<ServiceConfigurationBuilder> configDelegate)
        {
            _configDelegates.Add(configDelegate);
        }

        public void AddServiceEndpoint(Type implementedContract, Binding binding, Uri address, Uri listenUri)
        {
            _endpoints.Add(new ServiceEndpointConfiguration(implementedContract, binding, address, listenUri));
        }

        internal void ConfigureServiceBuilder(IServiceBuilder serviceBuilder)
        {
            foreach (var configDelegate in _configDelegates)
            {
                configDelegate(this);
            }

            serviceBuilder.AddService(_serviceType);
            foreach (var endpoint in _endpoints)
            {
                serviceBuilder.AddServiceEndpoint(_serviceType, endpoint.Contract, endpoint.Binding, endpoint.Address, endpoint.ListenUri);
            }

            _endpoints.Clear();
        }

        private struct ServiceEndpointConfiguration
        {
            public ServiceEndpointConfiguration(Type contract, Binding binding, Uri address, Uri listenUri)
            {
                Contract = contract;
                Binding = binding;
                Address = address;
                ListenUri = listenUri;
            }

            public Uri Address { get; set; }
            public Binding Binding { get; set; }
            public Type Contract { get; set; }
            public Uri ListenUri { get; set; }
        }
    }
}
