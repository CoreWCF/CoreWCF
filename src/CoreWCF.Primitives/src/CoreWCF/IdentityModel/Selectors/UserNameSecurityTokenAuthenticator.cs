using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CoreWCF.IdentityModel.Selectors
{
    internal abstract class UserNameSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        protected UserNameSecurityTokenAuthenticator()
        {
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return token is USerNameSecurityToken;
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            USerNameSecurityToken userNameToken = (USerNameSecurityToken)token;
            return ValidateUserNamePasswordCore(userNameToken.UserName, userNameToken.Password);
        }

        protected abstract ReadOnlyCollection<IAuthorizationPolicy> ValidateUserNamePasswordCore(string userName, string password);
    }
}
