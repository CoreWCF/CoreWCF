// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

internal static class CompressionTypeHelper
{
    public static bool IsDefined(CompressionType value)
    {
        return value == CompressionType.Gzip
               || value == CompressionType.Lz4
               || value == CompressionType.None
               || value == CompressionType.Snappy
               || value == CompressionType.Zstd;
    }
}
