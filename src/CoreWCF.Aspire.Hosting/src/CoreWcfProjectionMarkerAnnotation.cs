// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace CoreWCF.Aspire.Hosting;

/// <summary>
/// Marker annotation ensuring the explorer's service-to-environment projection callback is only
/// registered once, regardless of how many services are added.
/// </summary>
internal sealed class CoreWcfProjectionMarkerAnnotation : IResourceAnnotation
{
}
