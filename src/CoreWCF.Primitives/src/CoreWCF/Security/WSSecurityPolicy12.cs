// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security
{
    class WSSecurityPolicy12 : WSSecurityPolicy
    {
        public const string WsspNamespace = @"http://docs.oasis-open.org/ws-sx/ws-securitypolicy/200702";
        public const string SignedEncryptedSupportingTokensName = "SignedEncryptedSupportingTokens";
        public const string RequireImpliedDerivedKeysName = "RequireImpliedDerivedKeys";
        public const string RequireExplicitDerivedKeysName = "RequireExplicitDerivedKeys";
    }
}
