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
                Uri listenUri = httpRequest.HttpContext.Items["CoreWCF.Description.ServiceMetadataExtension.HttpGetImpl._listenUri"] as Uri;

                string host = null;
                int port = 0;

                // Get the host header
                HostString hostString = httpRequest.Host;
                if (!hostString.HasValue)
                {
                    return null;
                }

                host = hostString.Host;
                if (hostString.Port.HasValue)
                {
                    port = hostString.Port.Value;
                }
                else
                {
                    string hostUriString = string.Concat(listenUri.Scheme, "://", host);
                    if (!Uri.TryCreate(hostUriString, UriKind.Absolute, out Uri hostUri))
                    {
                        return null;
                    }

                    port = hostUri.Port;
                }

                return new UriBuilder(listenUri.Scheme, host, port).Uri;
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
            var mex = ServiceMetadataExtension.EnsureServiceMetadataExtension(serviceHostBase);
            mex.DynamicMetadataEndpointAddressProvider = new UseHostHeaderMetadataEndpointAddressProvider();
            mex.UpdatePortsByScheme = new ReadOnlyDictionary<string, int>(DefaultPortsByScheme);
        }
    }
}
