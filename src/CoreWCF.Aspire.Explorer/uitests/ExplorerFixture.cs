// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Xunit;

namespace CoreWCF.Aspire.Explorer.UITests;

/// <summary>
/// Brings up everything a UI test needs: the CoreWCF endpoints, the real explorer app as a child
/// process, and a headless browser. Shared across a test class so the ~5s start-up is paid once.
/// </summary>
public sealed class ExplorerFixture : IAsyncLifetime
{
    /// <summary>Name of the SOAP 1.2 service in the tree, shared with the tests that select from it.</summary>
    public const string Soap12ServiceName = "Calculator service (SOAP 1.2)";

    private readonly SoapServiceFixture _soapService = new();

    private Process? _explorer;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    /// <summary>Base address of the running explorer, for example <c>http://127.0.0.1:5xxxx</c>.</summary>
    public string ExplorerAddress { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _soapService.StartAsync();

        var port = FindFreePort();
        ExplorerAddress = $"http://127.0.0.1:{port}";
        _explorer = StartExplorer(port, _soapService.BaseAddress);
        await WaitForHealthAsync();

        // Installs on first run only. Done in code because the packaged installer is a PowerShell
        // script and pwsh is not guaranteed to be present (it is absent on a stock Windows box).
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
        if (exitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not install the Playwright chromium build (exit code {exitCode}). " +
                "Check network access, or run 'dotnet run --project <this project> install chromium' manually.");
        }

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    /// <summary>
    /// A fresh page per test. Blazor Server keeps one circuit per page, so sharing a page between
    /// tests would leak the selection and the filter from one test into the next.
    /// </summary>
    public async Task<IPage> NewPageAsync()
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
            // Keeps hover styling out of the screenshots and the geometry assertions stable.
            ReducedMotion = ReducedMotion.Reduce,
        });

        var page = await context.NewPageAsync();

        // Start listening before navigating, or the socket can open before the wait is armed.
        var circuitConnected = page.WaitForWebSocketAsync(new PageWaitForWebSocketOptions
        {
            Predicate = socket => socket.Url.Contains("_blazor"),
            Timeout = 60_000,
        });

        // Never wait for network idle: the SignalR socket means the page never reaches it.
        await page.GotoAsync(ExplorerAddress, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await circuitConnected;

        // Waiting for the markup is not enough. Blazor Server prerenders the whole tree server-side,
        // so the rows exist in the DOM well before the circuit can act on a click - and a click on
        // prerendered DOM is silently discarded, which makes tests fail seemingly at random.
        //
        // Blazor's client-side renderer stamps a `_bl_<guid>` attribute on an element at the moment
        // it wires up that element's handlers, so its presence is a precise "this row is live" signal.
        await page.WaitForFunctionAsync(
            """
            () => {
                const row = document.querySelector('[data-operation-id]');
                return !!row && Array.from(row.attributes).some(a => a.name.startsWith('_bl_'));
            }
            """,
            null,
            new PageWaitForFunctionOptions { Timeout = 30_000 });

        return page;
    }

    private static Process StartExplorer(int port, string soapBaseAddress)
    {
        var metadata = typeof(ExplorerFixture).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(a => a.Key, a => a.Value);

        var projectPath = Path.GetFullPath(metadata["ExplorerProjectPath"]!);
        var configuration = metadata["ExplorerConfiguration"] is { Length: > 0 } c ? c : "Debug";

        var startInfo = new ProcessStartInfo("dotnet")
        {
            // --no-build: the ProjectReference has already produced the output this runs.
            ArgumentList =
            {
                // --no-launch-profile: launchSettings.json would otherwise force Development and its
                // own fixed ports. Running in the default environment also keeps these tests honest
                // about the explorer serving its assets outside Development.
                "run", "--project", projectPath, "--no-build", "--no-launch-profile",
                "-c", configuration,
                "--urls", $"http://127.0.0.1:{port}",
            },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Deliberately not forced to Development. The explorer calls UseStaticWebAssets itself, so it
        // serves its stylesheets in any environment; leaving this at the default means these tests
        // would notice if that ever regressed and the UI came up unstyled.
        AddService(startInfo, 0, "Calculator service", soapBaseAddress, "/calc");
        AddService(startInfo, 1, Soap12ServiceName, soapBaseAddress, "/calc12");
        AddService(startInfo, 2, "Inventory service", soapBaseAddress, "/inventory");

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start the explorer process.");

        // Drained so the child never blocks on a full pipe.
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private static void AddService(ProcessStartInfo startInfo, int index, string name, string url, string path)
    {
        startInfo.Environment[$"CoreWcf__Services__{index}__Name"] = name;
        startInfo.Environment[$"CoreWcf__Services__{index}__Url"] = url;
        startInfo.Environment[$"CoreWcf__Services__{index}__Path"] = path;
    }

    private async Task WaitForHealthAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var deadline = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadline)
        {
            if (_explorer!.HasExited)
            {
                throw new InvalidOperationException(
                    $"The explorer exited with code {_explorer.ExitCode} before it became healthy.");
            }

            try
            {
                using var response = await client.GetAsync($"{ExplorerAddress}/health");
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(500);
        }

        throw new TimeoutException($"The explorer did not become healthy at {ExplorerAddress} within 90s.");
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync();
        }

        _playwright?.Dispose();

        if (_explorer is not null && !_explorer.HasExited)
        {
            // `dotnet run` launches the app as a child of itself, so the whole tree has to go.
            _explorer.Kill(entireProcessTree: true);
            _explorer.WaitForExit(10_000);
        }

        _explorer?.Dispose();
        await _soapService.DisposeAsync();
    }
}

[CollectionDefinition(nameof(ExplorerCollection))]
public sealed class ExplorerCollection : ICollectionFixture<ExplorerFixture>;
