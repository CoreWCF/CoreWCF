// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Confluent.Kafka;

namespace CoreWCF.Channels;

public sealed class KafkaMessageProperty : IMessageProperty
{
    private readonly IList<KafkaMessageHeader> _headers = new List<KafkaMessageHeader>();

    public static readonly string Name = "kafkaMessage";

    internal KafkaMessageProperty(ConsumeResult<byte[], byte[]> consumeResult)
    {
        foreach (IHeader messageHeader in consumeResult.Message.Headers)
        {
            _headers.Add(new KafkaMessageHeader(messageHeader.Key, messageHeader.GetValueBytes()));
        }

        PartitionKey = consumeResult.Message.Key;
        Topic = consumeResult.Topic;
    }

    public IMessageProperty CreateCopy() => this;

    public IReadOnlyCollection<KafkaMessageHeader> Headers => _headers as IReadOnlyCollection<KafkaMessageHeader>;
    public byte[] PartitionKey { get; }

    public string Topic { get; }
}
