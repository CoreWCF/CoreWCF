// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var builder = DistributedApplication.CreateBuilder(args);

// A project resource, not a container: this is what a consumer writes, and on Aspire 13.3+ the
// container tunnel lets the explorer container reach it on any platform. The 9.5.2 AppHost next door
// cannot do this - see its Program.cs - which is exactly the difference these two projects exist to
// cover.
var echoService = builder.AddProject<Projects.CoreWcfSampleService>("echo-service");

// Which explorer image to run. CI publishes it under a per-run tag and passes it in; with nothing set
// this falls back to the package default, which is what a consumer of the published package gets.
var explorerTag = builder.Configuration["CoreWcfExplorer:ImageTag"];

builder.AddCoreWcfExplorer("wcf-explorer", imageTag: string.IsNullOrWhiteSpace(explorerTag) ? null : explorerTag)
    .WithCoreWcfService(echoService, metadataPath: "/echo", name: "Echo service")
    .WithCoreWcfService(echoService, metadataPath: "/inventory", name: "Inventory service");

builder.Build().Run();
