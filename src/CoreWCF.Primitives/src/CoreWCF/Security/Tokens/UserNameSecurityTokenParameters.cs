// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    public class UserNameSecurityTokenParameters : SecurityTokenParameters
    {
        protected UserNameSecurityTokenParameters(UserNameSecurityTokenParameters other) : base(other)
        {
            RequireDerivedKeys = false;
        }

        public UserNameSecurityTokenParameters() : base()
        {
            RequireDerivedKeys = false;
        }

        protected internal override bool HasAsymmetricKey => false;
        protected internal override bool SupportsClientAuthentication => true;
        protected internal override bool SupportsServerAuthentication => false;
        protected internal override bool SupportsClientWindowsIdentity => true;

        protected override SecurityTokenParameters CloneCore()
        {
            return new UserNameSecurityTokenParameters(this);
        }

        protected internal override SecurityKeyIdentifierClause CreateKeyIdentifierClause(SecurityToken token, SecurityTokenReferenceStyle referenceStyle)
        {
            return CreateKeyIdentifierClause<SecurityKeyIdentifierClause, LocalIdKeyIdentifierClause>(token, referenceStyle);
        }

        protected internal override void InitializeSecurityTokenRequirement(SecurityTokenRequirement requirement)
        {
            requirement.TokenType = SecurityTokenTypes.UserName;
            requirement.RequireCryptographicToken = false;
        }
    }
}
