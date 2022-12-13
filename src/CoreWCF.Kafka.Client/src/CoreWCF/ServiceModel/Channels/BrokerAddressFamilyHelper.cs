// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

internal static class BrokerAddressFamilyHelper
{
    public static bool IsDefined(BrokerAddressFamily value)
    {
        return value == BrokerAddressFamily.Any
               || value == BrokerAddressFamily.V4
               || value == BrokerAddressFamily.V6;
    }
}
