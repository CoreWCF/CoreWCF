// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security
{
    public class SecurityTokenSpecification
    {
        public SecurityTokenSpecification(SecurityToken token, ReadOnlyCollection<IAuthorizationPolicy> tokenPolicies)
        {
            SecurityToken = token;
            SecurityTokenPolicies = tokenPolicies ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokenPolicies));
        }

        public SecurityToken SecurityToken { get; }

        public ReadOnlyCollection<IAuthorizationPolicy> SecurityTokenPolicies { get; }
    }
}
