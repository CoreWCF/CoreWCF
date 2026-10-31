// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace CoreWCF.Aspire.Hosting;

/// <summary>
/// A resource representing the CoreWCF SOAP service explorer - a companion web application, added to an
/// Aspire AppHost, that discovers the WSDL of referenced CoreWCF services and lets you browse their
/// contracts / operations and invoke them from the Aspire dashboard.
/// </summary>
/// <param name="name">The resource name shown in the dashboard.</param>
public sealed class CoreWcfExplorerResource(string name) : ContainerResource(name), IResourceWithServiceDiscovery
{
    /// <summary>The name of the HTTP endpoint that serves the explorer UI.</summary>
    public const string PrimaryEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>Gets the primary HTTP endpoint of the explorer web application.</summary>
    public EndpointReference PrimaryEndpoint => _primaryEndpoint ??= new(this, PrimaryEndpointName);
}
