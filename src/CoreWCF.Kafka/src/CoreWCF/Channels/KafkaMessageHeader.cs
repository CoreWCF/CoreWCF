// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels;

public sealed class KafkaMessageHeader
{
    public KafkaMessageHeader(string key, ReadOnlyMemory<byte> value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; }
    public ReadOnlyMemory<byte> Value { get; }
}
