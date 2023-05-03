// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

internal static class PartitionerHelper
{
    public static bool IsDefined(Partitioner value)
    {
        return value == Partitioner.Consistent
               || value == Partitioner.Murmur2
               || value == Partitioner.Random
               || value == Partitioner.ConsistentRandom
               || value == Partitioner.Murmur2Random;
    }
}
