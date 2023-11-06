// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF.Queue.Common;

namespace CoreWCF.Channels;

public class KafkaQueueMessageContext : QueueMessageContext
{
    private readonly IDictionary<string, object> _properties = new Dictionary<string, object>();

    public KafkaQueueMessageContext()
    {
        Properties = _properties;
    }

    public override IDictionary<string, object> Properties { get; }
}
