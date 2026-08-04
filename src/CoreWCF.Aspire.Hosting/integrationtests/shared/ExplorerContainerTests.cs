// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace CoreWcfExplorer.IntegrationTests;

/// <summary>
/// Covers the half of the hosting integration the unit tests cannot see. Those assert on the resource
/// model - image, registry, endpoint, environment projection - which says nothing about whether the
/// image exists, boots, or can reach the services it was pointed at.
/// </summary>
[Collection(nameof(ExplorerAppHostCollection))]
public sealed class ExplorerContainerTests(ExplorerAppHostFixture fixture)
{
    private readonly ExplorerAppHostFixture _fixture = fixture;

    [Fact]
    public async Task Explorer_serves_its_ui()
    {
        // The fixture already waited for Healthy, so reaching this point proves the container started
        // and /health answered. This adds that the app itself serves the UI, not just the probe.
        using var response = await _fixture.Client.GetAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Explorer_reads_the_metadata_of_every_registered_service()
    {
        var html = await _fixture.Client.GetStringAsync("/", TestContext.Current.CancellationToken);

        // This is the assertion the whole project exists for. The names below come from WSDL that the
        // explorer fetched over the network, from inside a container, at an address produced by
        // WithCoreWcfService's environment projection. Nothing short of a real container run can
        // establish that a container can reach a service running as a host process.
        //
        // The tree is present in the response because Blazor Server prerenders it: Index.OnInitializedAsync
        // loads every service up front and prerendering awaits it.
        Assert.Contains("Echo service", html);
        Assert.Contains("Inventory service", html);

        // Check the explorer's own error surface before asserting on contents. When a fetch fails the
        // tree renders "Failed to load WSDL: <reason>" for that service, and the reason is the whole
        // diagnosis - a name that will not resolve reads differently from a refused connection. Without
        // this, the same failure only ever reports "substring not found" against a truncated page.
        var failure = Regex.Match(html, "Failed to load WSDL:[^<]*");
        Assert.False(
            failure.Success,
            $"The explorer could not read a service's metadata from inside its container: {failure.Value.Trim()}");

        // Contract and operation names only exist if the WSDL parsed, not merely downloaded.
        Assert.Contains("IEchoService", html);
        Assert.Contains("GetOrderDetails", html);
        Assert.Contains("IInventoryService", html);
    }

    [Fact]
    public async Task Explorer_serves_its_stylesheets_from_the_container_image()
    {
        // The container runs in Production, where WebApplicationBuilder does not wire up static web
        // assets on its own. Without the explorer's explicit UseStaticWebAssets call the scoped-CSS
        // bundle 404s here and every Fluent component renders unstyled - invisible to the UI tests,
        // which run from source.
        using var response = await _fixture.Client.GetAsync(
            "/CoreWCF.Aspire.Explorer.styles.css", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(0, response.Content.Headers.ContentLength);
    }
}
