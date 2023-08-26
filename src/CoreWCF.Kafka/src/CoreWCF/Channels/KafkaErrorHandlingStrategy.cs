// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels;

public enum KafkaErrorHandlingStrategy
{
    Ignore,
    DeadLetterQueue
}

internal static class KafkaErrorHandlingStrategyHelper
{
    public static bool IsDefined(KafkaErrorHandlingStrategy value)
    {
        return value == KafkaErrorHandlingStrategy.Ignore
               || value == KafkaErrorHandlingStrategy.DeadLetterQueue;
    }
}
