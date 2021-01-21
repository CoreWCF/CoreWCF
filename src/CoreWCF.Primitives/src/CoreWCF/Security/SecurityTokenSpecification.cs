// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security
{
    public class SecurityTokenSpecification
    {
        SecurityToken token;
        ReadOnlyCollection<IAuthorizationPolicy> tokenPolicies;

        public SecurityTokenSpecification(SecurityToken token, ReadOnlyCollection<IAuthorizationPolicy> tokenPolicies)
        {
            this.token = token;
            if (tokenPolicies == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenPolicies));
            }
            this.tokenPolicies = tokenPolicies;
        }

        public SecurityToken SecurityToken
        {
            get { return token; }
        }

        public ReadOnlyCollection<IAuthorizationPolicy> SecurityTokenPolicies
        {
            get { return tokenPolicies; }
        }
    }
}
