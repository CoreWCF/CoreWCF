// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Dispatcher;

namespace CoreWCF.Description;

internal class EndpointAuthorizationBehavior : IEndpointBehavior
{
    public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
    {

    }

    public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
    {

    }

    public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
    {
        BindingParameterCollection parameters = new() { endpoint.Binding };
        var authorizationCapabilities = endpoint.Binding.GetProperty<IAuthorizationCapabilities>(parameters);
        if (authorizationCapabilities != null)
        {
            endpointDispatcher.DispatchRuntime.RequireClaimsPrincipalOnOperationContext =
                authorizationCapabilities.SupportsAuthorizationData;
            endpointDispatcher.DispatchRuntime.SupportsAuthorizationData =
                authorizationCapabilities.SupportsAuthorizationData;
        }
    }

    public void Validate(ServiceEndpoint endpoint)
    {

    }
}
