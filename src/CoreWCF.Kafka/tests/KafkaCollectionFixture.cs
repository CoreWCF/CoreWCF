// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Kafka.Tests.Helpers;
using Xunit;

namespace CoreWCF.Kafka.Tests;

/// <summary>
/// Collection definition for Kafka tests.
/// All tests in this collection will share the same KafkaContainerFixture instance,
/// reducing the number of Kafka and Zookeeper containers launched.
/// </summary>
[CollectionDefinition(nameof(KafkaCollection))]
public class KafkaCollection : ICollectionFixture<KafkaContainerFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
