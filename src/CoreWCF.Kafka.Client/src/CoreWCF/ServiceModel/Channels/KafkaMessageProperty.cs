// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ServiceModel.Channels;
using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

public sealed class KafkaMessageProperty : IMessageProperty
{
    private readonly IList<KafkaMessageHeader> _headers = new List<KafkaMessageHeader>();

    public static readonly string Name = "kafkaMessage";

    public IMessageProperty CreateCopy() => this;

    public IList<KafkaMessageHeader> Headers => _headers;
    public byte[] PartitionKey { get; set; }
}
