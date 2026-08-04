// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

var builder = DistributedApplication.CreateBuilder(args);

// The CoreWCF service to explore. It hosts two contracts with metadata (WSDL) enabled: IEchoService
// at /echo and /echo12 (SOAP 1.1 and 1.2 respectively) and IInventoryService at /inventory.
var echoService = builder.AddProject<Projects.CoreWcfSampleService>("echo-service");

// The SOAP explorer, run here as a project (no container image needed for local development), wired to
// the service above. In production use `builder.AddCoreWcfExplorer("wcf-explorer")` instead, which runs
// the published explorer container image; both expose the same WithCoreWcfService wiring.
// Registering both endpoints shows several services side by side in the explorer's tree.
builder.AddProject<Projects.CoreWCF_Aspire_Explorer>("wcf-explorer")
    .WithCoreWcfService(echoService, metadataPath: "/echo", name: "Echo service")
    .WithCoreWcfService(echoService, metadataPath: "/echo12", name: "Echo service (SOAP 1.2)")
    .WithCoreWcfService(echoService, metadataPath: "/inventory", name: "Inventory service");

builder.Build().Run();
