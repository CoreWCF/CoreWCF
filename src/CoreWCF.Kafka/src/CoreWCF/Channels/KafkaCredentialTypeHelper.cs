// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels;

internal static class KafkaCredentialTypeHelper
{
    public static bool IsDefined(KafkaCredentialType value)
    {
        return value == KafkaCredentialType.None
               || value == KafkaCredentialType.SslKeyPairCertificate
               || value == KafkaCredentialType.SaslPlain
               || value == KafkaCredentialType.SaslGssapi
               || value == KafkaCredentialType.SaslScramSha256
               || value == KafkaCredentialType.SaslScramSha512
               || value == KafkaCredentialType.SaslOAuthBearer;
    }

    public static bool IsSupported(KafkaCredentialType value)
    {
        return value == KafkaCredentialType.None
               || value == KafkaCredentialType.SslKeyPairCertificate
               || value == KafkaCredentialType.SaslPlain;
    }
}
