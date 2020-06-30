using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CoreWCF.Security
{
    class NonValidatingSecurityTokenAuthenticator<TTokenType> : SecurityTokenAuthenticator
    {
        public NonValidatingSecurityTokenAuthenticator()
            : base()
        { }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return (token is TTokenType);
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            return EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
        }
    }
}

