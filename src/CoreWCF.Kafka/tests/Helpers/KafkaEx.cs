// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests.Helpers;

internal static class KafkaEx
{
    private static string s_bootstrapServers = "localhost:9092";
    private static Lazy<IAdminClient> s_adminClient;

    static KafkaEx()
    {
        s_adminClient = new Lazy<IAdminClient>(() => new AdminClientBuilder(new AdminClientConfig { BootstrapServers = s_bootstrapServers }).Build());
    }

    public static void SetBootstrapServers(string bootstrapServers)
    {
        s_bootstrapServers = bootstrapServers;
        s_adminClient = new Lazy<IAdminClient>(() => new AdminClientBuilder(new AdminClientConfig { BootstrapServers = s_bootstrapServers }).Build());
    }

    public static string GetBootstrapServers() => s_bootstrapServers;

    private static IAdminClient AdminClient => s_adminClient.Value;

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
            BootstrapServers = s_bootstrapServers,
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
            long committed;

            long logEndOffset = watermarkOffsets.High.Value;

            if (tpo.Offset != Offset.Beginning && tpo.Offset != Offset.End && tpo.Offset != Offset.Stored && tpo.Offset != Offset.Unset)
            {
                committed = tpo.Offset.Value;
                lag += logEndOffset - committed;
                continue;
            }

            if (tpo.Offset == Offset.Unset)
            {
                var partitionLag = watermarkOffsets.High.Value - watermarkOffsets.Low.Value;
                lag += partitionLag;
                continue;
            }

            throw new NotSupportedException("Offset type not supported");
        }

        output.WriteLine($"{consumerGroup} has a lag of {lag} messages on topic {topicName}");
        return lag;
    }

    public static long GetMessageCount(ITestOutputHelper output, string topicName)
    {
        using var consumer = new ConsumerBuilder<Null, byte[]>(new ConsumerConfig
        {
            BootstrapServers = s_bootstrapServers,
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
