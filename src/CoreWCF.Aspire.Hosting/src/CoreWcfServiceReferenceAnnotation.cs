// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;

namespace CoreWCF.Aspire.Hosting;

/// <summary>
/// Annotation recording a CoreWCF service that the explorer should surface. One is added per call to
/// <c>WithCoreWcfService</c>. The explorer reads these (projected into environment variables) to know
/// which services to fetch WSDL from.
/// </summary>
/// <param name="serviceName">Display name of the service in the explorer UI.</param>
/// <param name="endpoint">The service endpoint whose base address hosts the metadata.</param>
/// <param name="metadataPath">
/// The path, relative to the endpoint base address, at which the service is hosted (for example
/// <c>/Service.svc</c>). The explorer requests <c>{endpoint}{metadataPath}?singleWsdl</c>.
/// </param>
internal sealed class CoreWcfServiceReferenceAnnotation(
    string serviceName,
    EndpointReference endpoint,
    string metadataPath) : IResourceAnnotation
{
    public string ServiceName { get; } = serviceName;

    public EndpointReference Endpoint { get; } = endpoint;

    public string MetadataPath { get; } = metadataPath;
}
