// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.ServiceModel.Channels;

public enum KafkaCredentialType
{
    None,
    SslKeyPairCertificate,
    SaslGssapi,
    SaslPlain,
    SaslScramSha256,
    SaslScramSha512,
    SaslOAuthBearer,
}
