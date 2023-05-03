// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Confluent.Kafka;

namespace CoreWCF.Channels;

internal static class SaslMechanismHelper
{
    public static bool IsDefined(SaslMechanism value)
    {
        return value == SaslMechanism.Plain
               || value == SaslMechanism.Gssapi
               || value == SaslMechanism.ScramSha256
               || value == SaslMechanism.ScramSha512
               || value == SaslMechanism.OAuthBearer;
    }
}
