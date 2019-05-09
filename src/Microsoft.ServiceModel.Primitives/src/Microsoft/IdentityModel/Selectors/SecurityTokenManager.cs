using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.IdentityModel.Selectors
{
    internal abstract class SecurityTokenManager
    {
        public abstract SecurityTokenProvider CreateSecurityTokenProvider(SecurityTokenRequirement tokenRequirement);
        internal abstract SecurityTokenSerializer CreateSecurityTokenSerializer(SecurityTokenVersion version);
        public abstract SecurityTokenAuthenticator CreateSecurityTokenAuthenticator(SecurityTokenRequirement tokenRequirement, out SecurityTokenResolver outOfBandTokenResolver);
    }
}
