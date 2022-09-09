// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.Channels;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Description
{
    public class UseRequestHeadersForMetadataAddressBehavior : IServiceBehavior
    {
        internal class UseHostHeaderMetadataEndpointAddressProvider : IMetadataEndpointAddressProvider
        {
            public Uri GetEndpointAddress(HttpRequest httpRequest)
            {
                // Get the host header
                HostString hostString = httpRequest.Host;
                if (!hostString.HasValue)
                {
                    return null;
                }

                if (hostString.Port.HasValue && Uri.TryCreate($"http://{hostString.Host}:{hostString.Port}", UriKind.Absolute, out Uri metadataEndpointAddress))
                {
                    return metadataEndpointAddress;
                }

                return Uri.TryCreate($"http://{hostString.Host}", UriKind.Absolute, out metadataEndpointAddress)
                    ? metadataEndpointAddress
                    : null;
            }
        }

        private Dictionary<string, int> _defaultPortsByScheme;

        public IDictionary<string, int> DefaultPortsByScheme
        {
            get
            {
                if (_defaultPortsByScheme == null)
                {
                    _defaultPortsByScheme = new Dictionary<string, int>();
                }

                return _defaultPortsByScheme;
            }
        }

        void IServiceBehavior.Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase) { }
        void IServiceBehavior.AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters) { }

        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription serviceDescription,
            ServiceHostBase serviceHostBase)
        {
            ServiceMetadataExtension.EnsureServiceMetadataExtension(serviceHostBase);
            var mex = serviceHostBase.Extensions.Find<ServiceMetadataExtension>();
            mex.DynamicMetadataEndpointAddressProvider = new UseHostHeaderMetadataEndpointAddressProvider();
            mex.UpdatePortsByScheme = new ReadOnlyDictionary<string, int>(DefaultPortsByScheme);
        }
    }
}
