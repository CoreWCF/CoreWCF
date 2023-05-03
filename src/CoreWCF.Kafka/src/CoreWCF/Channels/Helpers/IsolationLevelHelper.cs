// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

namespace CoreWCF.Channels;

internal static class IsolationLevelHelper
{
    public static bool IsDefined(IsolationLevel value)
    {
        return value == IsolationLevel.ReadCommitted
               || value == IsolationLevel.ReadUncommitted;
    }
}
