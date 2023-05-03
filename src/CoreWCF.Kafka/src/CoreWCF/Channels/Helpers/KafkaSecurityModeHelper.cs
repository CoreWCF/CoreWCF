// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels;

internal static class KafkaSecurityModeHelper
{
    public static bool IsDefined(KafkaSecurityMode value)
    {
        return value == KafkaSecurityMode.None
               || value == KafkaSecurityMode.Transport
               || value == KafkaSecurityMode.TransportCredentialOnly;
    }
}
