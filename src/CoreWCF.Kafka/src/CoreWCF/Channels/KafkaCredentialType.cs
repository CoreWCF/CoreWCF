// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels;

public enum KafkaCredentialType
{
    None,
    SslKeyPairCertificate,
    // not supported yet because not tested with integration test.
    SaslGssapi, 
    SaslPlain,
    // not supported yet because not tested with integration test.
    SaslScramSha256,
    // not supported yet because not tested with integration test.
    SaslScramSha512,
    // not supported yet because not tested with integration test.
    SaslOAuthBearer,
}
