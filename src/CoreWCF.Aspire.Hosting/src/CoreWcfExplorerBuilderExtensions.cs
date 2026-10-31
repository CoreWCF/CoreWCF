// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.ApplicationModel;
using CoreWCF.Aspire.Hosting;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding the CoreWCF SOAP service explorer to an Aspire application and wiring
/// CoreWCF services to it.
/// </summary>
public static class CoreWcfExplorerBuilderExtensions
{
    internal const string DefaultHttpEndpointName = "http";
    private const int ExplorerContainerPort = 8080;

    /// <summary>
    /// Adds the CoreWCF SOAP service explorer to the application. The explorer is a companion web
    /// application (shipped as a container image) that surfaces the WSDL of the services registered via
    /// <see cref="WithCoreWcfService{T}"/> and lets you browse and invoke their operations from the
    /// Aspire dashboard.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name. Defaults to <c>wcf-explorer</c>.</param>
    /// <param name="port">Optional host port to expose the explorer on. When omitted a port is assigned.</param>
    /// <param name="imageTag">Optional explorer container image tag override.</param>
    /// <returns>A resource builder for the explorer, for further configuration.</returns>
    public static IResourceBuilder<CoreWcfExplorerResource> AddCoreWcfExplorer(
        this IDistributedApplicationBuilder builder,
        string name = "wcf-explorer",
        int? port = null,
        string? imageTag = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);

        var resource = new CoreWcfExplorerResource(name);

        return builder.AddResource(resource)
            .WithImage(CoreWcfExplorerImageTags.Image, imageTag ?? CoreWcfExplorerImageTags.Tag)
            .WithImageRegistry(CoreWcfExplorerImageTags.Registry)
            .WithHttpEndpoint(port: port, targetPort: ExplorerContainerPort, name: CoreWcfExplorerResource.PrimaryEndpointName)
            .WithHttpHealthCheck("/health")
            .WithUrlForEndpoint(CoreWcfExplorerResource.PrimaryEndpointName, url => url.DisplayText = "SOAP Explorer");
    }

    /// <summary>
    /// Registers a CoreWCF service with the explorer. The service's metadata (WSDL) is fetched from
    /// <c>{endpoint}{metadataPath}?singleWsdl</c> and its contracts / operations become browsable and
    /// invokable in the explorer UI.
    /// </summary>
    /// <typeparam name="TExplorer">The explorer resource type (a container or, in samples/tests, a project).</typeparam>
    /// <typeparam name="TService">The CoreWCF service resource type (any resource exposing endpoints).</typeparam>
    /// <param name="explorer">The explorer resource builder.</param>
    /// <param name="service">The CoreWCF service to explore.</param>
    /// <param name="metadataPath">
    /// The path, relative to the service endpoint, at which the service is hosted (for example
    /// <c>/Service.svc</c>). Defaults to <c>/</c>.
    /// </param>
    /// <param name="name">Optional display name. Defaults to the service resource name.</param>
    /// <param name="endpointName">Optional endpoint name to target. Defaults to <c>http</c>.</param>
    public static IResourceBuilder<TExplorer> WithCoreWcfService<TExplorer, TService>(
        this IResourceBuilder<TExplorer> explorer,
        IResourceBuilder<TService> service,
        string metadataPath = "/",
        string? name = null,
        string? endpointName = null)
        where TExplorer : IResourceWithEnvironment, IResourceWithWaitSupport
        where TService : class, IResourceWithEndpoints
    {
        ArgumentNullException.ThrowIfNull(explorer);
        ArgumentNullException.ThrowIfNull(service);

        var endpoint = service.GetEndpoint(endpointName ?? DefaultHttpEndpointName);

        explorer.WithAnnotation(new CoreWcfServiceReferenceAnnotation(
            name ?? service.Resource.Name,
            endpoint,
            NormalizePath(metadataPath)));

        // Inject service-discovery info for the endpoint so the explorer can reach the service in every
        // launch mode, and only start once the service is available.
        explorer.WithReference(endpoint);
        explorer.WaitFor(service);

        EnsureServiceProjection(explorer);
        return explorer;
    }

    /// <summary>
    /// Registers the environment-variable projection callback exactly once. It projects every
    /// <see cref="CoreWcfServiceReferenceAnnotation"/> into <c>CoreWcf__Services__N__{Name,Url,Path}</c>
    /// entries the explorer binds to at startup.
    /// </summary>
    private static void EnsureServiceProjection<T>(IResourceBuilder<T> explorer)
        where T : IResourceWithEnvironment
    {
        if (explorer.Resource.Annotations.OfType<CoreWcfProjectionMarkerAnnotation>().Any())
        {
            return;
        }

        explorer.WithAnnotation(new CoreWcfProjectionMarkerAnnotation());
        explorer.WithEnvironment(context =>
        {
            var services = explorer.Resource.Annotations
                .OfType<CoreWcfServiceReferenceAnnotation>()
                .ToArray();

            for (var i = 0; i < services.Length; i++)
            {
                var service = services[i];
                context.EnvironmentVariables[$"CoreWcf__Services__{i}__Name"] = service.ServiceName;
                context.EnvironmentVariables[$"CoreWcf__Services__{i}__Url"] = service.Endpoint;
                context.EnvironmentVariables[$"CoreWcf__Services__{i}__Path"] = service.MetadataPath;
            }
        });
    }

    private static string NormalizePath(string metadataPath)
    {
        if (string.IsNullOrWhiteSpace(metadataPath) || metadataPath == "/")
        {
            return "/";
        }

        var path = metadataPath.Trim();
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        return path.TrimEnd('/') is { Length: > 0 } trimmed ? trimmed : "/";
    }
}
