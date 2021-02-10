// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public interface IEndpointBehavior
    {
        void AddBindingParameters(ServiceEndpoint endpoint, Channels.BindingParameterCollection bindingParameters);
        void ApplyClientBehavior(CoreWCF.Description.ServiceEndpoint endpoint, CoreWCF.Dispatcher.ClientRuntime clientRuntime);
        void ApplyDispatchBehavior(ServiceEndpoint endpoint, Dispatcher.EndpointDispatcher endpointDispatcher);
        void Validate(ServiceEndpoint endpoint);
    }
}