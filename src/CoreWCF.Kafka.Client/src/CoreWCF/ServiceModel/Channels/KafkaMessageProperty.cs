// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.ServiceModel.Channels;

public sealed class KafkaMessageProperty
{
    public static readonly string Name = "CoreWCF.ServiceModel.Channels.KafkaMessageProperty";

    public IList<KafkaMessageHeader> Headers { get; } = new List<KafkaMessageHeader>();
    public byte[] PartitionKey { get; set; }
}
