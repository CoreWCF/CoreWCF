// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    public class UseRequestHeadersForMetadataAddressBehavior : IServiceBehavior
    {
        private Dictionary<string, int> _defaultPortsByScheme;

        public UseRequestHeadersForMetadataAddressBehavior()
        {
        }

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
        void IServiceBehavior.ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase) { }
    }
}
