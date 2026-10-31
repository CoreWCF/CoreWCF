# Hosting integration tests

End-to-end tests for `CoreWCF.Aspire.Hosting`. They start a real Aspire AppHost, which runs the real
explorer **container image** and a real CoreWCF service, and then drive the explorer over HTTP.

Both ends of the supported Aspire range are covered, because they reach the service in genuinely
different ways:

| Project | Aspire | The CoreWCF service is… |
| --- | --- | --- |
| `CoreWcfExplorer.IntegrationTests.AppHost` + `CoreWCF.Aspire.Hosting.IntegrationTests` | 9.5.2 (the floor) | a **container**, alongside the explorer |
| `CoreWcfExplorer.IntegrationTests.Aspire13.AppHost` + `CoreWCF.Aspire.Hosting.Aspire13.IntegrationTests` | 13.4.6 (current) | a **project resource**, reached through the container tunnel |

The assertions live in `shared/` and are compiled into both, so neither line gets an easier test than
the other. Only the fixture differs, because the AppHost entry point has to be a compile-time type.

**Why the floor uses a containerised service.** On Aspire 9.x a container cannot reach a proxied
project endpoint on Linux. The address handed to the container is `host.docker.internal`, which does
not resolve there, and mapping it onto the bridge gateway only moves the failure along to
`Connection refused`, because the proxy is not listening on that interface. Putting both resources on
the container network — where they address each other by resource name — is the only way to exercise
the explorer against a real service on that line.

**Why 13.4.6 uses a project resource.** Aspire 13.3 introduced the container tunnel, which makes
container-to-host work on every platform, so the explorer resolves the service as
`http://aspire.dev.internal:<port>` via a tunnel container instead. A project resource is what
consumers actually write, and nothing else in the repo exercises that topology.

Two test projects rather than one multi-targeted project: `DistributedApplicationTestingBuilder`
loads the AppHost entry point in-process, so a single assembly cannot host both Aspire lines without
their assemblies colliding.

## Why these are separate from the unit tests

`CoreWCF.Aspire.Hosting.Tests` next door asserts on the *resource model* — image coordinates, endpoint,
the `CoreWcf__Services__N__*` environment projection. All of that can be correct while the integration
is still broken, because none of it establishes that:

- the image exists and boots at all;
- the `/health` probe `AddCoreWcfExplorer` attaches actually answers;
- the endpoint addresses `WithCoreWcfService` projects are reachable **from inside a container** —
  the service runs as a host process, the explorer does not;
- the explorer serves its static web assets outside Development, which is the only environment the
  container ever runs in.

Only a real container run shows those, and each has been a live bug at some point.

## Running them

They need a **Linux container runtime** and are Linux-container only. Build both images, tag the
explorer under the registry the package hardcodes, then point the tests at the tag:

```bash
dotnet publish src/CoreWCF.Aspire.Explorer/src/CoreWCF.Aspire.Explorer.csproj \
  -c Release -t:PublishContainer -p:ContainerImageTag=local-test
docker tag corewcf/aspire-explorer:local-test ghcr.io/corewcf/aspire-explorer:local-test

dotnet publish src/CoreWCF.Aspire.Hosting/samples/CoreWcfSampleService/CoreWcfSampleService.csproj \
  -c Release -t:PublishContainer -p:ContainerImageTag=local-test

# The 9.5.2 floor - both resources as containers.
CoreWcfExplorer__ImageTag=local-test CoreWcfSampleService__ImageTag=local-test \
  dotnet test src/CoreWCF.Aspire.Hosting/integrationtests/CoreWCF.Aspire.Hosting.IntegrationTests

# Aspire 13.4.6 - the service runs as a project, so it needs no image. Requires a .NET 10 SDK.
CoreWcfExplorer__ImageTag=local-test \
  dotnet test src/CoreWCF.Aspire.Hosting/integrationtests/CoreWCF.Aspire.Hosting.Aspire13.IntegrationTests
```

Aspire uses a locally tagged image as-is; nothing is pulled. With `CoreWcfExplorer__ImageTag` unset
the AppHost falls back to the package default (`latest`), which is what a consumer of the published
package gets — so an unset variable tests the published image, not a local build.

To confirm the topology rather than just the result, inspect the explorer container while the tests
run and look at `CoreWcf__Services__0__Url`:

| Run | Expected address |
| --- | --- |
| 9.5.2 | `http://echo-service:8080` — container network DNS |
| 13.4.6 | `http://aspire.dev.internal:<port>` — the tunnel, with a `dcptun_*` container alongside |

A `host.docker.internal` address in either means the host is back in the path, and the tests will
pass on Docker Desktop while failing on Linux — which is exactly how this was originally missed.

## Why they are not in the CI test matrix

`…IntegrationTests.csproj` deliberately does not match `CoreWCF.*.Tests.csproj`, the glob
`.github/actions/run-tests` sweeps. That matrix runs on Windows and Linux across five target
frameworks and has no container runtime; these tests need one, and need it in Linux mode. They run
in their own ubuntu-only job instead.

Renaming either project so that it ends in `.Tests` would silently enrol it in every CI leg.
