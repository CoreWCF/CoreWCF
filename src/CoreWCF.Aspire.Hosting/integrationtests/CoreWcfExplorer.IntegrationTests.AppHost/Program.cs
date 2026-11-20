// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var builder = DistributedApplication.CreateBuilder(args);

// Which images to run. CI publishes both under a per-run tag and passes them in; with nothing set
// these fall back to the defaults, which is what a consumer of the published package gets.
var explorerTag = builder.Configuration["CoreWcfExplorer:ImageTag"];
var serviceTag = builder.Configuration["CoreWcfSampleService:ImageTag"] ?? "latest";

// The service whose metadata the explorer reads, run as a container rather than as a project.
//
// Not a stylistic choice. This AppHost is pinned to the Aspire 9.5.2 support floor, and on that line
// a container cannot reach a proxied project endpoint on Linux: the address handed to the container
// is host.docker.internal, which does not resolve there, and mapping it to the bridge gateway only
// moves the failure on to "connection refused" because the proxy is not listening on that interface.
// Aspire 13.3 solved this properly with the container tunnel. Below it, the only way to exercise the
// explorer against a real service is to put both on the container network, where they address each
// other by resource name and no host hop is involved.
var echoService = builder.AddContainer("echo-service", "corewcf/sample-service", serviceTag)
    .WithHttpEndpoint(targetPort: 8080, name: "http");

builder.AddCoreWcfExplorer("wcf-explorer", imageTag: string.IsNullOrWhiteSpace(explorerTag) ? null : explorerTag)
    .WithCoreWcfService(echoService, metadataPath: "/echo", name: "Echo service")
    .WithCoreWcfService(echoService, metadataPath: "/inventory", name: "Inventory service");

builder.Build().Run();
