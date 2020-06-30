namespace CoreWCF.Security.Tokens
{
    using CoreWCF.IdentityModel.Selectors;
    using CoreWCF;
    using CoreWCF.IdentityModel.Tokens;
    using CoreWCF.Security;
    using CoreWCF.IdentityModel;

    public class UserNameSecurityTokenParameters : SecurityTokenParameters
    {
        protected UserNameSecurityTokenParameters(UserNameSecurityTokenParameters other)
            : base(other)
        {
            base.RequireDerivedKeys = false;
        }

        public UserNameSecurityTokenParameters()
            : base()
        {
            base.RequireDerivedKeys = false;
        }

        internal protected override bool HasAsymmetricKey { get { return false; } }
        internal protected override bool SupportsClientAuthentication { get { return true; } }
        internal protected override bool SupportsServerAuthentication { get { return false; } }
        internal protected override bool SupportsClientWindowsIdentity { get { return true; } }

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
