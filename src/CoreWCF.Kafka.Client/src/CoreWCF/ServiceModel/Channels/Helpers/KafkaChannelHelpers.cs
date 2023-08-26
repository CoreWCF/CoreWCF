// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.ServiceModel;
using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

internal static class KafkaChannelHelpers
{
    public static Exception ConvertProduceException(ProduceException<Null, byte[]> produceException)
    {
        return new CommunicationException(
            string.Format(CultureInfo.CurrentCulture,
                "An error ({0}: {1}) occurred while transmitting data.", produceException.Error.Code, produceException.Error.Reason),
            produceException);
    }

    public static Exception ConvertError(Error error)
    {
        return new CommunicationException(
            string.Format(CultureInfo.InvariantCulture,
                "An error ({0}: {1}) occurred while transmitting data.", error.Code, error.Reason));
    }
}
