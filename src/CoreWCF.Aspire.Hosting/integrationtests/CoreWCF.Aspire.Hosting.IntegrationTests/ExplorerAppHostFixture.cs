// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire.Hosting.Testing;
using Xunit;

namespace CoreWcfExplorer.IntegrationTests;

/// <summary>
/// Binds the shared fixture to the AppHost pinned at the Aspire 9.5.2 support floor, where the CoreWCF
/// service runs as a container alongside the explorer.
/// </summary>
public sealed class ExplorerAppHostFixture : ExplorerAppHostFixtureBase
{
    protected override Task<IDistributedApplicationTestingBuilder> CreateBuilderAsync()
        => DistributedApplicationTestingBuilder.CreateAsync<Projects.CoreWcfExplorer_IntegrationTests_AppHost>();
}

[CollectionDefinition(nameof(ExplorerAppHostCollection))]
public sealed class ExplorerAppHostCollection : ICollectionFixture<ExplorerAppHostFixture>;
