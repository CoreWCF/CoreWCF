// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Aspire.Explorer.Model;

/// <summary>
/// Identifies a CoreWCF service the explorer should surface. Bound from the
/// <c>CoreWcf:Services</c> configuration section injected by the Aspire hosting integration.
/// </summary>
public sealed class CoreWcfServiceDescriptor
{
    /// <summary>Display name of the service.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Base endpoint address of the service (for example <c>http://localhost:5000</c>).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Path, relative to <see cref="Url"/>, at which the service is hosted (for example <c>/Service.svc</c>).</summary>
    public string Path { get; set; } = "/";

    /// <summary>The absolute address of the service endpoint (invocation target).</summary>
    public string EndpointAddress
    {
        get
        {
            var baseUrl = Url.TrimEnd('/');
            var path = Path;
            if (string.IsNullOrEmpty(path) || path == "/")
            {
                return baseUrl;
            }

            if (!path.StartsWith('/'))
            {
                path = "/" + path;
            }

            return baseUrl + path;
        }
    }

    /// <summary>The URL that returns the flattened single-document WSDL.</summary>
    public string SingleWsdlUrl => EndpointAddress + "?singleWsdl";
}
