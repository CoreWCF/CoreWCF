// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests.Helpers;

internal static class KafkaEx
{
    private static Lazy<IAdminClient> AdminClient => new(() => new AdminClientBuilder(new AdminClientConfig { BootstrapServers = "localhost:9092" }).Build());

    public static async Task CreateTopicAsync(ITestOutputHelper output, string name)
    {
        output.WriteLine($"Create topic {name}");
        await AdminClient.Value.CreateTopicsAsync(new[] { new TopicSpecification() { Name = name, NumPartitions = 4 } }, new CreateTopicsOptions()
        {
            OperationTimeout = TimeSpan.FromSeconds(30)
        });
    }

    public static async Task DeleteTopicAsync(ITestOutputHelper output, string name)
    {
        output.WriteLine($"Delete topic {name}");
        await AdminClient.Value.DeleteTopicsAsync(new [] { name }, new DeleteTopicsOptions
        {
            OperationTimeout = TimeSpan.FromSeconds(30)
        });
    }

    public static long GetConsumerLag(ITestOutputHelper output, string consumerGroup, string topicName)
    {
        using var consumer = new ConsumerBuilder<Null, byte[]>(new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = consumerGroup
        }).Build();

        var meta = AdminClient.Value.GetMetadata(TimeSpan.FromSeconds(30));

        var topicPartitions = meta.Topics.Where(x => x.Topic == topicName)
            .SelectMany(x => x.Partitions)
            .Select(x => new TopicPartition(topicName, x.PartitionId))
            .ToList();

        consumer.Assign(topicPartitions);

        List<TopicPartitionOffset> tpos = consumer.Committed(TimeSpan.FromSeconds(30));

        long lag = 0;
        foreach (var tpo in tpos)
        {
            WatermarkOffsets watermarkOffsets = consumer.QueryWatermarkOffsets(tpo.TopicPartition, TimeSpan.FromSeconds(30));
            long committed = tpo.Offset.Value;
            if (committed == Offset.Unset)
            {
                continue;
            }
            long logEndOffset = watermarkOffsets.High.Value;
            lag += logEndOffset - committed;
        }

        output.WriteLine($"{consumerGroup} has a lag of {lag} messages on topic {topicName}");
        return lag;
    }

    public static long GetMessageCount(ITestOutputHelper output, string topicName)
    {
        using var consumer = new ConsumerBuilder<Null, byte[]>(new ConsumerConfig
        {
            BootstrapServers = "localhost:9092",
            GroupId = Guid.NewGuid().ToString(),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnablePartitionEof = true
        }).Build();

        var meta = AdminClient.Value.GetMetadata(TimeSpan.FromSeconds(30));

        var topicPartitions = meta.Topics.Where(x => x.Topic == topicName)
            .SelectMany(x => x.Partitions)
            .Select(x => new TopicPartition(topicName, x.PartitionId))
            .ToList();

        consumer.Assign(topicPartitions);

        List<TopicPartitionOffset> tpos = consumer.Committed(TimeSpan.FromSeconds(30));

        long messageCount = 0;
        foreach (var tpo in tpos)
        {
            WatermarkOffsets watermarkOffsets = consumer.QueryWatermarkOffsets(tpo.TopicPartition, TimeSpan.FromSeconds(30));
            long logEndOffset = watermarkOffsets.High.Value;
            messageCount += logEndOffset;
        }

        output.WriteLine($"{messageCount} messages found in topic {topicName}");
        return messageCount;
    }
}
