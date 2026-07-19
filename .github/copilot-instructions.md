# Copilot Instructions for CoreWCF

CoreWCF is a port of the server-side of Windows Communication Foundation (WCF) to .NET Core. It enables existing WCF services to migrate to modern .NET.

## Build and Test

The solution file is `CoreWCF.sln` at the repository root. (A separate `CoreWCFTemplates.sln` builds the templates project.)

```shell
# Build
dotnet build CoreWCF.sln

# Run all tests
dotnet test CoreWCF.sln

# Run tests for a specific project
dotnet test src/CoreWCF.Http/tests/CoreWCF.Http.Tests.csproj

# Run a single test by fully-qualified name
dotnet test src/CoreWCF.Http/tests/CoreWCF.Http.Tests.csproj --filter "FullyQualifiedName~BasicScenariosTest.BasicScenariosAndOps"

# Run tests for a specific target framework
dotnet test src/CoreWCF.Http/tests/CoreWCF.Http.Tests.csproj -f net8.0
```

Test target frameworks: `net8.0`, `net9.0`, `net10.0`, and `net472` (Windows only). All library projects target `netstandard2.0`.

Some integration tests (Kafka, RabbitMQ) require Docker containers. These use Testcontainers and start automatically when the tests run.

## Architecture

### Project Layout

- **CoreWCF.Primitives** — Core library: service contracts, channels, dispatching, security, DI integration. Everything else depends on this.
- **CoreWCF.Http** — HTTP/HTTPS transport via ASP.NET Core Kestrel.
- **CoreWCF.NetTcp** / **CoreWCF.NetNamedPipe** / **CoreWCF.UnixDomainSocket** — Frame-based transports built on `CoreWCF.NetFramingBase`.
- **CoreWCF.Kafka**, **CoreWCF.RabbitMQ**, **CoreWCF.MSMQ** — Message queue transports built on `CoreWCF.Queue` (shared queue abstraction).
- **CoreWCF.Kafka.Client**, **CoreWCF.RabbitMQ.Client** — Client-side libraries for queue transports.
- **CoreWCF.ConfigurationManager** — XML-based service configuration (WCF `<system.serviceModel>` compat).
- **CoreWCF.Metadata** — WSDL metadata generation.
- **CoreWCF.WebHttp** — REST-style WebHttp binding support.
- **CoreWCF.BuildTools** — Roslyn source generators and analyzers (OperationInvokerGenerator, OperationParameterInjectionGenerator).

### Shared Code via Source Inclusion

`src/Common/src/` contains shared source files that are **compiled into** each library project (not a shared project reference). This is controlled by the `IncludeCommonCode` property in `Directory.Build.props`/`.targets`. When editing shared code, be aware changes affect all consuming projects.

### How Services are Hosted

CoreWCF integrates with ASP.NET Core's hosting model:

```csharp
services.AddServiceModelServices();  // Register CoreWCF in DI

app.UseServiceModel(builder =>
{
    builder.AddService<MyService>();
    builder.AddServiceEndpoint<MyService, IMyService>(new BasicHttpBinding(), "/Service.svc");
});
```

Each transport has its own extension method for host configuration (e.g., `.UseNetTcp()` on `IWebHostBuilder`).

### Versioning

Uses [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) with `version.json` at the repo root. Version is `{major}.{minor}.{patch}-alpha.{height}` during development. Do not set versions manually in `.csproj` files.

## Conventions

### File Headers

All C# files must include this license header (enforced by `.editorconfig`):

```csharp
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
```

### Naming

- Private/internal fields: `_camelCase`
- Static fields: `s_camelCase`
- Constants: `PascalCase`
- Avoid `var` — use explicit types (configured as suggestion in `.editorconfig`)
- Avoid `this.` qualification

### Central Package Management

NuGet package versions are managed centrally in `Directory.Packages.props`. When adding a dependency, add the `<PackageVersion>` there and reference it without a version in the `.csproj`:

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="MyPackage" Version="1.2.3" />

<!-- In .csproj -->
<PackageReference Include="MyPackage" />
```

### Testing Patterns

- Test framework: **xUnit** with `Microsoft.NET.Test.Sdk`.
- Test projects auto-reference `src/Common/UnitTests.Common` for shared helpers (via `ReferenceCommonTestProject` in `Directory.Build.props`).
- Integration tests use `ServiceHelper.CreateWebHostBuilder<TStartup>(output)` to create in-process ASP.NET Core hosts with auto-assigned ports. Each transport has its own `ServiceHelper` in its test project's `Helpers/` directory.
- Tests receive `ITestOutputHelper` via constructor injection for logging.
- Platform-specific test attributes (defined in `src/Common/UnitTests.Common/CustomXunitAttributes.cs`):
  - `[WindowsOnlyFact]` / `[WindowsOnlyTheory]`
  - `[LinuxOnlyFact]`
  - `[NetCoreOnlyFact]`
  - `[WindowsNetCoreOnlyFact]` / `[WindowsNetCoreOnlyTheory]`
  - `[SkipOnGeneratedOperationInvokerFact]` — skips when generated operation invokers are active (class-based contracts unsupported)
- Service contracts for tests go in `ServiceContract/` directories; implementations in `Services/`.
- Test `Startup` classes configure bindings and endpoints, mirroring real-world host configuration.

### Project Structure Conventions

- Each module (`CoreWCF.{Name}`) has `src/` and `tests/` subdirectories.
- Test projects use `$(TestTargetFrameworks)` from `Directory.Build.props` for multi-TFM testing.
- Some test projects disable concurrent test execution (`RunUnitTestsConcurrently=false`) for tests that use shared resources (Metadata, MSMQ, Queue, Kafka, HTTP on net472).

### PRs and Branches

PRs must target the `main` branch. Release branches follow the pattern `release/v{version}`.

### Strong Naming

All non-test assemblies are strong-name signed using `corewcf.snk` at the repo root.
