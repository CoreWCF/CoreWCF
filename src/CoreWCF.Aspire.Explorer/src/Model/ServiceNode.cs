// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Aspire.Explorer.Model;

/// <summary>
/// The page's view state for one configured service: its descriptor plus the outcome of loading the
/// WSDL. Holding this on one object keeps the tree from having to thread four parallel dictionaries
/// (model / error / loading / expanded) through every component.
/// </summary>
public sealed class ServiceNode(CoreWcfServiceDescriptor descriptor)
{
    public CoreWcfServiceDescriptor Descriptor { get; } = descriptor;

    public string Name => Descriptor.Name;

    /// <summary>The parsed WSDL, once loaded successfully.</summary>
    public WsdlModel? Model { get; set; }

    /// <summary>The failure message, if the last load attempt failed.</summary>
    public string? Error { get; set; }

    public bool IsLoading { get; set; }

    public bool IsExpanded { get; set; }

    /// <summary>True once a load attempt has completed, successfully or not.</summary>
    public bool IsLoaded => Model is not null || Error is not null;
}
