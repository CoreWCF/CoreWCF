// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests.Helpers;

public class IntegrationTest : IAsyncLifetime
{
    private readonly bool _useDlq;
    protected ITestOutputHelper Output { get; }
    protected string Topic { get; }
    protected string ConsumerGroup { get; }
    protected string DeadLetterQueueTopic { get; }

    protected IntegrationTest(ITestOutputHelper output, bool useDlq = false, [CallerMemberName] string callerName = "")
    {
        _useDlq = useDlq;
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
