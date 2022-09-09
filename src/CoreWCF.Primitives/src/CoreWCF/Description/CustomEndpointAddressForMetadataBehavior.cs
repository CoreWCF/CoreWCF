// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    public class CustomEndpointAddressForMetadataBehavior : IServiceBehavior
    {
        private readonly IMetadataEndpointAddressProvider _provider;

        public CustomEndpointAddressForMetadataBehavior(IMetadataEndpointAddressProvider provider)
        {
            _provider = provider;
        }

        void IServiceBehavior.Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {

        }

        void IServiceBehavior.AddBindingParameters(ServiceDescription serviceDescription,
            ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints,
            BindingParameterCollection bindingParameters)
        {

        }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription serviceDescription,
            ServiceHostBase serviceHostBase)
        {
            var mex = ServiceMetadataExtension.EnsureServiceMetadataExtension(serviceHostBase);
            mex.DynamicMetadataEndpointAddressProvider = _provider;
        }
    }
}
