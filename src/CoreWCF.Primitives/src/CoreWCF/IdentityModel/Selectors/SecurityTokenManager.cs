// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel.Selectors
{
    public abstract class SecurityTokenManager
    {
        public abstract SecurityTokenProvider CreateSecurityTokenProvider(SecurityTokenRequirement tokenRequirement);
        internal abstract SecurityTokenSerializer CreateSecurityTokenSerializer(SecurityTokenVersion version);
        public abstract SecurityTokenAuthenticator CreateSecurityTokenAuthenticator(SecurityTokenRequirement tokenRequirement, out SecurityTokenResolver outOfBandTokenResolver);
    }
}
