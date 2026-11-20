// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWcfExplorer.IntegrationTests;

/// <summary>
/// Starts an AppHost once for a whole test class and exposes an HttpClient addressed at the explorer.
/// Starting it means DCP really launches the explorer container, so the cost is paid once rather than
/// per test.
/// <para>
/// Shared by the Aspire 9.5.2 and 13.4.6 test projects. Only the entry point differs, and it has to be
/// a compile-time type argument, so each project supplies its own by overriding
/// <see cref="CreateBuilderAsync"/>. Everything the tests actually observe is identical, which is the
/// point: both Aspire lines are held to the same assertions.
/// </para>
/// </summary>
public abstract class ExplorerAppHostFixtureBase : IAsyncLifetime
{
    /// <summary>Matches the resource name both AppHosts pass to <c>AddCoreWcfExplorer</c>.</summary>
    public const string ExplorerResourceName = "wcf-explorer";

    /// <summary>The endpoint name AddCoreWcfExplorer gives the explorer's HTTP endpoint.</summary>
    private const string ExplorerEndpointName = "http";

    /// <summary>Generous: a cold run pulls base images and starts containers.</summary>
    private static readonly TimeSpan s_startupTimeout = TimeSpan.FromMinutes(5);

    private DistributedApplication? _app;

    /// <summary>An HttpClient addressed at the explorer's endpoint, via Aspire's service discovery.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Creates the testing builder for this project's AppHost.</summary>
    protected abstract Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync();

    public async ValueTask InitializeAsync()
    {
        var builder = await CreateBuilderAsync();

        _app = await builder.BuildAsync();

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();

        using var cts = new CancellationTokenSource(s_startupTimeout);
        await _app.StartAsync(cts.Token);

        // Healthy rather than merely Running. The resource only turns healthy once the probe that
        // AddCoreWcfExplorer attaches - WithHttpHealthCheck("/health") - has actually answered from
        // inside the container, so this single wait covers "the image boots" and "the health wiring
        // is real" at the same time.
        await notifications.WaitForResourceHealthyAsync(ExplorerResourceName, cts.Token);

        Client = _app.CreateHttpClient(ExplorerResourceName, ExplorerEndpointName);
    }

    public async ValueTask DisposeAsync()
    {
        Client?.Dispose();

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }
}
