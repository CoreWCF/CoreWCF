// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

namespace CoreWCF.Channels;
internal static class SecurityProtocolHelper
{
    public static bool IsDefined(SecurityProtocol value)
    {
        return value == SecurityProtocol.Plaintext
               || value == SecurityProtocol.Ssl
               || value == SecurityProtocol.SaslPlaintext
               ||value == SecurityProtocol.SaslSsl;
    }
}
