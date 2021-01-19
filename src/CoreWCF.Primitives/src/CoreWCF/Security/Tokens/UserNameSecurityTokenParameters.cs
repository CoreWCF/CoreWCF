using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    public class UserNameSecurityTokenParameters : SecurityTokenParameters
    {
        protected UserNameSecurityTokenParameters(UserNameSecurityTokenParameters other) : base(other)
        {
            base.RequireDerivedKeys = false;
        }

        public UserNameSecurityTokenParameters() : base()
        {
            base.RequireDerivedKeys = false;
        }

        internal protected override bool HasAsymmetricKey => false;
        internal protected override bool SupportsClientAuthentication => true;
        internal protected override bool SupportsServerAuthentication => false;
        internal protected override bool SupportsClientWindowsIdentity => true;

        protected override SecurityTokenParameters CloneCore()
        {
            return new UserNameSecurityTokenParameters(this);
        }

        internal protected override SecurityKeyIdentifierClause CreateKeyIdentifierClause(SecurityToken token, SecurityTokenReferenceStyle referenceStyle)
        {
            return this.CreateKeyIdentifierClause<SecurityKeyIdentifierClause, LocalIdKeyIdentifierClause>(token, referenceStyle);
        }

        protected internal override void InitializeSecurityTokenRequirement(SecurityTokenRequirement requirement)
        {
            requirement.TokenType = SecurityTokenTypes.UserName;
            requirement.RequireCryptographicToken = false;
        }
    }
}
