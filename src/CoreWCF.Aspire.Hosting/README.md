# CoreWCF.Aspire.Hosting

A [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) hosting integration that adds a **SOAP
service explorer** to the Aspire dashboard, so the CoreWCF services orchestrated by your AppHost can be
browsed and invoked directly from the developer control plane (DCP).

The explorer is a companion web application (`CoreWCF.Aspire.Explorer`) that, for every registered
service, fetches its WSDL (`?singleWsdl`), lists its contracts and operations, and lets you edit a
pre-filled SOAP envelope and invoke the operation — a lightweight WCF Test Client / SoapUI, embedded in
your Aspire run.

📖 **[Explorer UI guide, with screenshots](../../Documentation/AspireExplorer/readme.md)** — the tree,
filtering, the request editors, invoking, faults, themes and keyboard shortcuts.

## Usage

In your Aspire AppHost:

```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Your CoreWCF service(s), with metadata (WSDL) enabled.
var echo = builder.AddProject<Projects.MyCoreWcfService>("echo-service");

// Add the explorer and point it at the services to explore.
builder.AddCoreWcfExplorer("wcf-explorer")
       .WithCoreWcfService(echo, metadataPath: "/echo", name: "Echo service");

builder.Build().Run();
```

`AddCoreWcfExplorer` runs the explorer from its published container image. Open the **SOAP Explorer**
URL shown on the explorer resource in the dashboard.

### The container image

The explorer image is `ghcr.io/corewcf/aspire-explorer`, built from
`src/CoreWCF.Aspire.Explorer` with SDK container publishing and pushed by the CI workflow:

| Tag | Points at |
| --- | --- |
| `latest` | the most recent build of `main` — what `AddCoreWcfExplorer` resolves by default |
| `main` | the same, as a named moving tag |
| `sha-<short>` | one specific commit; immutable |

Pass `imageTag:` to pin a different one. Every build of the image is exercised end to end before it is
pushed — see `integrationtests/`, which starts a real AppHost against it.

### Requirements on the CoreWCF service

The service must expose metadata over HTTP GET so the explorer can read the WSDL:

```csharp
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
// ...
var metadata = app.Services.GetRequiredService<ServiceMetadataBehavior>();
metadata.HttpGetEnabled = true; // or HttpsGetEnabled
```

## Supported Aspire versions

The package ships a single `lib/net8.0` assembly and depends on `Aspire.Hosting >= 9.5.2` with **no
upper bound**. Aspire 13.x still ships `lib/net8.0`, so the same assembly serves both the 9.x and 13.x
lines, and none of the APIs this integration uses changed between them.

Tested in CI against **Aspire 9.5.2 and 13.4.6**: the `net8.0`/`net9.0` test legs resolve 9.5.2 and the
`net10.0`/`net11.0` legs resolve 13.4.6, exercising the same shipped assembly against both.

Note that the .NET version requirement comes from **your AppHost**, not from this package: the Aspire
13.x `Aspire.AppHost.Sdk` requires a .NET 10 SDK, while the 9.x line works on .NET 8.

## API

- `IDistributedApplicationBuilder.AddCoreWcfExplorer(name = "wcf-explorer", port?, imageTag?)` — adds the
  explorer resource.
- `IResourceBuilder<TExplorer>.WithCoreWcfService(service, metadataPath = "/", name?, endpointName?)` —
  registers a CoreWCF service (any resource exposing an endpoint) with the explorer. The service's
  endpoint URL and metadata path are injected into the explorer via `CoreWcf:Services` configuration and
  the service is added as a reference the explorer waits for.

## Running the sample

See `samples/` for a runnable AppHost that hosts an Echo service and the explorer (run here as a project,
so no container image is required for local development):

```bash
dotnet run --project samples/CoreWcfSample.AppHost
```
