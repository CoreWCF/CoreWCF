using Microsoft.IdentityModel.Policy;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Microsoft.ServiceModel.Security
{
    internal class SecurityTokenSpecification
    {
        SecurityToken token;
        ReadOnlyCollection<IAuthorizationPolicy> tokenPolicies;

        public SecurityTokenSpecification(SecurityToken token, ReadOnlyCollection<IAuthorizationPolicy> tokenPolicies)
        {
            this.token = token;
            if (tokenPolicies == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("tokenPolicies");
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
