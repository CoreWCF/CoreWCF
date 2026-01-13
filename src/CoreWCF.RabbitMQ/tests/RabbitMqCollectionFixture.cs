// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.RabbitMQ.Tests.Helpers;
using Xunit;

namespace CoreWCF.RabbitMQ.Tests;

/// <summary>
/// Collection definition for RabbitMQ tests.
/// All tests in this collection will share the same RabbitMqContainerFixture instance,
/// reducing the number of RabbitMQ containers launched.
/// </summary>
[CollectionDefinition(nameof(RabbitMqCollection))]
public class RabbitMqCollection : ICollectionFixture<RabbitMqContainerFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
