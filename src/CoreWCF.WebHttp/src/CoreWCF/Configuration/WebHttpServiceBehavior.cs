// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.Channels;
using CoreWCF.Collections.Generic;
using CoreWCF.Description;

namespace CoreWCF.Configuration
{
    public class WebHttpServiceBehavior : IServiceBehavior
    {
        private readonly IServiceProvider _serviceProvider;

        public WebHttpServiceBehavior(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            foreach (ServiceEndpoint endpoint in serviceDescription.Endpoints)
            {
                WebMessageEncodingBindingElement webEncodingBindingElement = endpoint.Binding.CreateBindingElements().Find<WebMessageEncodingBindingElement>();
                if (webEncodingBindingElement != null)
                {
                    var behaviors = (KeyedByTypeCollection<IEndpointBehavior>)endpoint.EndpointBehaviors;
                    behaviors.Add(new WebHttpBehavior(_serviceProvider));
                }
            }
        }
    }
}
