// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels;

internal static class KafkaSecurityModeHelper
{
    public static bool IsDefined(KafkaSecurityMode value)
    {
        return (int)value is >= (int)KafkaSecurityMode.None and <= (int)KafkaSecurityMode.Message;
    }

    public static bool IsSupported(KafkaSecurityMode value)
    {
        return value switch
        {
            KafkaSecurityMode.Transport => true,
            KafkaSecurityMode.TransportCredentialOnly => true,
            KafkaSecurityMode.None => true,
            _ => false,
        };
    }
}
