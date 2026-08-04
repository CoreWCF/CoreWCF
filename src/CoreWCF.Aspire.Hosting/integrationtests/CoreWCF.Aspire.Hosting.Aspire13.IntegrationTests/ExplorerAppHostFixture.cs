// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Testing;
using Xunit;

namespace CoreWcfExplorer.IntegrationTests;

/// <summary>
/// Binds the shared fixture to the Aspire 13.4.6 AppHost, where the CoreWCF service runs as a project
/// resource and the explorer container reaches it through Aspire's container tunnel.
/// </summary>
public sealed class ExplorerAppHostFixture : ExplorerAppHostFixtureBase
{
    protected override Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync()
        => DistributedApplicationTestingBuilder.CreateAsync<Projects.CoreWcfExplorer_IntegrationTests_Aspire13_AppHost>();
}

[CollectionDefinition(nameof(ExplorerAppHostCollection))]
public sealed class ExplorerAppHostCollection : ICollectionFixture<ExplorerAppHostFixture>;
