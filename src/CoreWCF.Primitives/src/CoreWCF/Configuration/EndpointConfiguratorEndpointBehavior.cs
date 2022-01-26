// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;

namespace CoreWCF.Configuration
{
    internal class EndpointConfiguratorEndpointBehavior : IEndpointBehavior
    {
        private readonly Action<ServiceEndpoint> _configureEndpoint;

        public EndpointConfiguratorEndpointBehavior(Action<ServiceEndpoint> configureEndpoint)
        {
            _configureEndpoint = configureEndpoint;
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {

        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {

        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            _configureEndpoint(endpoint);
        }

        public void Validate(ServiceEndpoint endpoint)
        {

        }
    }
}
