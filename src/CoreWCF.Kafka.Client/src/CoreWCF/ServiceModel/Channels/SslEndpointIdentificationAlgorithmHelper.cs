// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

internal static class SslEndpointIdentificationAlgorithmHelper
{
    public static bool IsDefined(SslEndpointIdentificationAlgorithm value)
    {
        return value == SslEndpointIdentificationAlgorithm.Https
               || value == SslEndpointIdentificationAlgorithm.None;
    }
}
