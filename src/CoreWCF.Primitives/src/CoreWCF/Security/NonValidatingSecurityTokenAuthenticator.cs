using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using System.Collections.ObjectModel;

namespace CoreWCF.Security
{
    internal class NonValidatingSecurityTokenAuthenticator<TTokenType> : SecurityTokenAuthenticator
    {
        public NonValidatingSecurityTokenAuthenticator() : base()
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

