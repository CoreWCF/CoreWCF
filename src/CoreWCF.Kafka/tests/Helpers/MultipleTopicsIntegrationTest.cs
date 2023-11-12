// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests.Helpers;

public class MultipleTopicsIntegrationTest : IAsyncLifetime
{
    private readonly bool _useDlq;
    private readonly List<string> _topics = new();
    protected ITestOutputHelper Output { get; }
    protected IReadOnlyList<string> Topics => _topics;
    protected string ConsumerGroup { get; }
    protected string DeadLetterQueueTopic { get; }
    protected string TopicRegex { get; }

    protected MultipleTopicsIntegrationTest(ITestOutputHelper output, bool useDlq = false)
    {
        _useDlq = useDlq;
        Output = output;
        string topic = $"topic-{Guid.NewGuid()}";
        TopicRegex = $"^{topic}_.*";
        for (int i = 0; i < 10; i++)
        {
            _topics.Add($"{topic}_{i}");
        }

        if (_useDlq)
        {
            DeadLetterQueueTopic = $"{topic}-DLQ";
        }
        ConsumerGroup = $"cg-{Guid.NewGuid()}";
    }

    public async Task InitializeAsync()
    {
        foreach (var topic in _topics)
        {
            await KafkaEx.CreateTopicAsync(Output, topic);
        }
        if (_useDlq)
        {
            await KafkaEx.CreateTopicAsync(Output, DeadLetterQueueTopic);
        }
    }

    public async Task DisposeAsync()
    {
        foreach (var topic in _topics)
        {
            await KafkaEx.DeleteTopicAsync(Output, topic);
        }
        if (_useDlq)
        {
            await KafkaEx.DeleteTopicAsync(Output, DeadLetterQueueTopic);
        }
    }
}
