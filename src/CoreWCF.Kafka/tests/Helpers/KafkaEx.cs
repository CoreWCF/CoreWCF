// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests.Helpers;

internal static class KafkaEx
{
    private static readonly string s_containerName = "broker";

    public static async Task CreateTopicAsync(ITestOutputHelper output, string name)
    {
        // build a command to create the topic
        string command = $"kafka-topics --bootstrap-server broker:9092 --create --topic {name} --partitions 1 --replication-factor 1";
        await DockerEx.RunAsync(s_containerName, command);
        output.WriteLine($"Topic {name} created");
    }

    public static async Task DeleteTopicAsync(ITestOutputHelper output, string name)
    {
        // build a command to delete the topic
        string command = $"kafka-topics --bootstrap-server broker:9092 --delete --topic {name}";
        await DockerEx.RunAsync(s_containerName, command);
        output.WriteLine($"Topic {name} deleted");
    }

    public static async Task<long> GetConsumerLagAsync(ITestOutputHelper output, string consumerGroup, string topicName)
    {
        // build a command to get the lag
        string command = $"kafka-consumer-groups --bootstrap-server broker:9092 --group {consumerGroup} --timeout 30000 --describe";
        IReadOnlyList<string> outputLines = await DockerEx.RunAsync(s_containerName, command);
        // Parse the below output to get the lag
        // GROUP                                   TOPIC                                      PARTITION  CURRENT-OFFSET  LOG-END-OFFSET  LAG             CONSUMER-ID     HOST            CLIENT-ID
        // cg-b20b1e1d-8115-489d-b4ce-c853107f4638 topic-b2002787-06a0-43ba-9614-909301ead2b3 0          100             100             0               -               -               -

        // skip the first line
        outputLines = outputLines.Skip(1).ToList();
        // find the line with the topic
        string line = outputLines.FirstOrDefault(x => x.Contains(topicName));
        Assert.NotNull(line);
        // split the line
        string[] parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(9, parts.Length);
        // get the lag
        long lag = long.Parse(parts[5]);
        output.WriteLine($"{consumerGroup} has a lag of {lag} messages on topic {topicName}");
        return lag;
    }

    public static async Task<long> GetMessageCountAsync(ITestOutputHelper output, string topicName)
    {
        string command = $"kafka-run-class kafka.tools.GetOffsetShell --broker-list broker:9092 --topic {topicName} --time -1";
        IReadOnlyList<string> outputLines = await DockerEx.RunAsync(s_containerName, command);
        // Parse the below output to get the lag (last values after ':')
        // topic-b2002787-06a0-43ba-9614-909301ead2b3:0:100

        // find the line with the topic
        string line = outputLines.FirstOrDefault(x => x.Contains(topicName));
        Assert.NotNull(line);
        // split the line
        string[] parts = line.Split(":");
        Assert.Equal(3, parts.Length);
        // get the message count
        long messageCount = long.Parse(parts[2]);
        output.WriteLine($"{messageCount} messages found in topic {topicName}");
        return messageCount;
    }
}
