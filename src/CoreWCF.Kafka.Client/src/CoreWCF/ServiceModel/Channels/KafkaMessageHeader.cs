// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.ServiceModel.Channels;

public sealed class KafkaMessageHeader
{
    public KafkaMessageHeader(string key, byte[] value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; }
    public byte[] Value { get; }
}
