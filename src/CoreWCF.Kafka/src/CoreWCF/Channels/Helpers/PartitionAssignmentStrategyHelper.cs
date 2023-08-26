// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

namespace CoreWCF.Channels;

internal static class PartitionAssignmentStrategyHelper
{
    public static bool IsDefined(PartitionAssignmentStrategy value)
    {
        return value == PartitionAssignmentStrategy.Range
               || value == PartitionAssignmentStrategy.CooperativeSticky
               || value == PartitionAssignmentStrategy.RoundRobin;

    }
}
