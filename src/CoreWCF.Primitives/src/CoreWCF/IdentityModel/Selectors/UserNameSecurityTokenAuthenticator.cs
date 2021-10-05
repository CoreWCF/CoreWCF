// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Selectors
{
    internal abstract class UserNameSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        protected UserNameSecurityTokenAuthenticator()
        {
        }

        protected override bool CanValidateTokenCore(SecurityToken token) => token is UserNameSecurityToken;

        protected override ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token)
        {
            UserNameSecurityToken userNameToken = (UserNameSecurityToken)token;
            return ValidateUserNamePasswordCoreAsync(userNameToken.UserName, userNameToken.Password);
        }

        [Obsolete("Implementers should override ValidateUserNamePasswordCoreAsync.")]
        protected virtual ReadOnlyCollection<IAuthorizationPolicy> ValidateUserNamePasswordCore(string userName, string password) => throw new NotImplementedException(SR.SynchronousUserNameTokenValidationIsDeprecated);

        protected virtual ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateUserNamePasswordCoreAsync(string userName, string password)
        {
            // Default to calling sync implementation to support existing derived types which haven't overridden this method
            return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(ValidateUserNamePasswordCore(userName, password));
        }
    }
}
