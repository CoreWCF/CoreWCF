// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel.Channels;

namespace CoreWCF.ServiceModel.Channels
{
    internal class KafkaConstants
    {
        public const string Scheme = "net.kafka";

        static KafkaConstants()
        {
            DefaultMessageEncoderFactory = new TextMessageEncodingBindingElement().CreateMessageEncoderFactory();
        }

        internal static MessageEncoderFactory DefaultMessageEncoderFactory { get; }
    }
}
