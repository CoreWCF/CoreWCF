// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF.Aspire.Explorer.Model;
using Microsoft.Extensions.Configuration;

namespace CoreWCF.Aspire.Explorer.Services;

/// <summary>
/// The set of CoreWCF services the explorer knows about, bound from the <c>CoreWcf:Services</c>
/// configuration section that the Aspire hosting integration injects as environment variables.
/// </summary>
public sealed class ServiceCatalog
{
    public ServiceCatalog(IConfiguration configuration)
    {
        Services = configuration.GetSection("CoreWcf:Services").Get<List<CoreWcfServiceDescriptor>>()
            ?? new List<CoreWcfServiceDescriptor>();
    }

    public IReadOnlyList<CoreWcfServiceDescriptor> Services { get; }
}
