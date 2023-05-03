// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

namespace CoreWCF.Channels;

internal static class AutoOffsetResetHelper
{
    public static bool IsDefined(AutoOffsetReset value)
    {
        return value == AutoOffsetReset.Earliest
               || value == AutoOffsetReset.Error
               || value == AutoOffsetReset.Latest;
    }
}
