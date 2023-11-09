// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.ServiceModel;
using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

internal static class KafkaChannelHelpers
{
    public static Exception ConvertProduceException(ProduceException<byte[], byte[]> produceException)
    {
        if (produceException.Error.IsLocalError && produceException.Error.Code == ErrorCode.Local_MsgTimedOut)
        {
            throw new TimeoutException(SR.KafkaSendTimeout, produceException);
        }

        return new CommunicationException(
            string.Format(CultureInfo.CurrentCulture,
                "An error ({0}: {1}) occurred while transmitting data.", produceException.Error.Code, produceException.Error.Reason),
            produceException);
    }
}
