// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public interface IServiceBehavior
    {
        void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase);
        void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, System.Collections.ObjectModel.Collection<ServiceEndpoint> endpoints, Channels.BindingParameterCollection bindingParameters);
        void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase);
    }
}