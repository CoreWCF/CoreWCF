// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading.Tasks;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using CoreWCF.Aspire.Hosting;
using Xunit;

namespace CoreWCF.Aspire.Hosting.Tests;

public class CoreWcfExplorerBuilderExtensionsTests
{
    [Fact]
    public void AddCoreWcfExplorer_AddsExplorerResourceWithDefaults()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());

        var explorer = builder.AddCoreWcfExplorer();

        Assert.Equal("wcf-explorer", explorer.Resource.Name);
        Assert.IsType<CoreWcfExplorerResource>(explorer.Resource);

        var image = Assert.Single(explorer.Resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("ghcr.io", image.Registry);
        Assert.Equal("corewcf/aspire-explorer", image.Image);
        Assert.Equal("latest", image.Tag);

        var endpoint = Assert.Single(
            explorer.Resource.Annotations.OfType<EndpointAnnotation>(),
            e => e.Name == CoreWcfExplorerResource.PrimaryEndpointName);
        Assert.Equal("http", endpoint.UriScheme);
    }

    [Fact]
    public void AddCoreWcfExplorer_HonorsCustomNameAndImageTag()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());

        var explorer = builder.AddCoreWcfExplorer("soap-ui", imageTag: "1.2.3");

        Assert.Equal("soap-ui", explorer.Resource.Name);
        var image = Assert.Single(explorer.Resource.Annotations.OfType<ContainerImageAnnotation>());
        Assert.Equal("1.2.3", image.Tag);
    }

    [Fact]
    public void WithCoreWcfService_AddsWaitAnnotation()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var service = builder.AddContainer("echo", "example/echo")
            .WithHttpEndpoint(targetPort: 8080, name: "http");

        var explorer = builder.AddCoreWcfExplorer()
            .WithCoreWcfService(service, metadataPath: "/echo", name: "Echo service");

        Assert.NotEmpty(explorer.Resource.Annotations.OfType<WaitAnnotation>());
    }

    [Fact]
    public async Task WithCoreWcfService_ProjectsServicesIntoEnvironment()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var service = builder.AddContainer("echo", "example/echo")
            .WithHttpEndpoint(targetPort: 8080, name: "http");

        var explorer = builder.AddCoreWcfExplorer()
            .WithCoreWcfService(service, metadataPath: "/echo", name: "Echo service");

        var env = await explorer.Resource.GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("Echo service", env["CoreWcf__Services__0__Name"]);
        Assert.Equal("/echo", env["CoreWcf__Services__0__Path"]);
        Assert.Contains("echo", env["CoreWcf__Services__0__Url"]);
    }

    [Fact]
    public async Task WithCoreWcfService_RegistersProjectionCallbackOnlyOnce()
    {
        var builder = DistributedApplication.CreateBuilder(Array.Empty<string>());
        var first = builder.AddContainer("first", "example/first").WithHttpEndpoint(targetPort: 8080, name: "http");
        var second = builder.AddContainer("second", "example/second").WithHttpEndpoint(targetPort: 8080, name: "http");

        var explorer = builder.AddCoreWcfExplorer()
            .WithCoreWcfService(first, name: "First")
            .WithCoreWcfService(second, name: "Second");

        var env = await explorer.Resource.GetEnvironmentVariableValuesAsync(DistributedApplicationOperation.Publish);

        Assert.Equal("First", env["CoreWcf__Services__0__Name"]);
        Assert.Equal("Second", env["CoreWcf__Services__1__Name"]);
        // No third, duplicated projection.
        Assert.False(env.ContainsKey("CoreWcf__Services__2__Name"));
    }
}
