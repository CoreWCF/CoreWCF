// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels;

public enum KafkaDeliverySemantics
{
    AtLeastOnce,
    AtMostOnce
}

internal static class KafkaDeliverySemanticsHelper
{
    public static bool IsDefined(KafkaDeliverySemantics value)
    {
        return value == KafkaDeliverySemantics.AtLeastOnce
               || value == KafkaDeliverySemantics.AtMostOnce;
    }
}
