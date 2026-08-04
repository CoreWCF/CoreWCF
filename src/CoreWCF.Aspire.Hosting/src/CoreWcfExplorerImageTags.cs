// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Aspire.Hosting;

/// <summary>
/// Default container image coordinates for the CoreWCF SOAP service explorer companion application.
/// The image is produced from the <c>CoreWCF.Aspire.Explorer</c> project via SDK container publishing.
/// </summary>
internal static class CoreWcfExplorerImageTags
{
    public const string Registry = "ghcr.io";

    public const string Image = "corewcf/aspire-explorer";

    public const string Tag = "latest";
}
