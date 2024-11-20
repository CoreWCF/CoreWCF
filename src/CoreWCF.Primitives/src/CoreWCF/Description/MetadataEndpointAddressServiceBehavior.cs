// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    public class MetadataEndpointAddressServiceBehavior : IServiceBehavior
    {
        private readonly IMetadataEndpointAddressProvider _provider;

        public MetadataEndpointAddressServiceBehavior(IMetadataEndpointAddressProvider provider)
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
            if (serviceDescription != null && serviceDescription.Endpoints != null)
            {
                for (int i = 0; i < serviceDescription.Endpoints.Count; i++)
                {
                    var address = serviceDescription.Endpoints[i].Address.Uri.AbsolutePath;
                    var mex = ServiceMetadataExtension.EnsureServiceMetadataExtension(serviceHostBase, address);
                    mex.DynamicMetadataEndpointAddressProvider = _provider;
                }
            }
        }
    }
}
