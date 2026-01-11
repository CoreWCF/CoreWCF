// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests.Helpers;

public class IntegrationTest : IAsyncLifetime, IClassFixture<KafkaContainerFixture>
{
    private readonly bool _useDlq;
    private readonly KafkaContainerFixture _containerFixture;
    protected ITestOutputHelper Output { get; }
    protected string Topic { get; }
    protected string ConsumerGroup { get; }
    protected string DeadLetterQueueTopic { get; }

    protected IntegrationTest(ITestOutputHelper output, KafkaContainerFixture containerFixture, bool useDlq = false)
    {
        _useDlq = useDlq;
        _containerFixture = containerFixture;
        Output = output;
        Topic = $"topic-{Guid.NewGuid()}";
        if (_useDlq)
        {
            DeadLetterQueueTopic = $"{Topic}-DLQ";
        }
        ConsumerGroup = $"cg-{Guid.NewGuid()}";
    }

    public async Task InitializeAsync()
    {
        // Set the bootstrap servers for KafkaEx after the fixture has initialized
        if (!string.IsNullOrEmpty(_containerFixture.BootstrapServers))
        {
            KafkaEx.SetBootstrapServers(_containerFixture.BootstrapServers);
        }
        
        // Set the container ID for pause/unpause operations
        if (!string.IsNullOrEmpty(_containerFixture.KafkaContainerId))
        {
            KafkaEx.SetKafkaContainerId(_containerFixture.KafkaContainerId);
        }
        
        await KafkaEx.CreateTopicAsync(Output, Topic);
        if (_useDlq)
        {
            await KafkaEx.CreateTopicAsync(Output, DeadLetterQueueTopic);
        }
    }

    public async Task DisposeAsync()
    {
        await KafkaEx.DeleteTopicAsync(Output, Topic);
        if (_useDlq)
        {
            await KafkaEx.DeleteTopicAsync(Output, DeadLetterQueueTopic);
        }
    }
}
