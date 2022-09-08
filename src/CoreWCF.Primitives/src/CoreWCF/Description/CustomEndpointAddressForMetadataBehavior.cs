// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    public class CustomEndpointAddressForMetadataBehavior : IServiceBehavior
    {
        public IEndpointAddressForMetadataProvider Provider { get; }

        public CustomEndpointAddressForMetadataBehavior(IEndpointAddressForMetadataProvider provider)
        {
            Provider = provider;
        }

        void IServiceBehavior.Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase) { }
        void IServiceBehavior.AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters) { }
        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase) { }
    }
}
