using Microsoft.IdentityModel.Policy;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Microsoft.IdentityModel.Selectors
{
    internal abstract class UserNameSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        protected UserNameSecurityTokenAuthenticator()
        {
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return token is UserNameSecurityToken;
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            UserNameSecurityToken userNameToken = (UserNameSecurityToken)token;
            return ValidateUserNamePasswordCore(userNameToken.UserName, userNameToken.Password);
        }

        protected abstract ReadOnlyCollection<IAuthorizationPolicy> ValidateUserNamePasswordCore(string userName, string password);
    }
}
