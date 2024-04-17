// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.ServiceModel;
using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels;

internal static class KafkaChannelHelpers
{
    public static Exception ConvertKafkaException(KafkaException kafkaException)
    {
        if (kafkaException.Error.IsLocalError && kafkaException.Error.Code == ErrorCode.Local_MsgTimedOut)
        {
            throw new TimeoutException(SR.KafkaSendTimeout, kafkaException);
        }

        return new CommunicationException(
            string.Format(CultureInfo.CurrentCulture,
                "An error ({0}: {1}) occurred while transmitting data.", kafkaException.Error.Code, kafkaException.Error.Reason),
            kafkaException);
    }
}
