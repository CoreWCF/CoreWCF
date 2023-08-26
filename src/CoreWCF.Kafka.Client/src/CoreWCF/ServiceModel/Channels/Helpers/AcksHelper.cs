// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

internal static class AcksHelper
{
    public static bool IsDefined(Acks value)
    {
        return value == Acks.All
               || value == Acks.Leader
               || value == Acks.None;
    }
}
