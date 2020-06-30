using System;
using CoreWCF;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.IdentityModel.Policy;
using System.Xml;

namespace CoreWCF.Security.Tokens
{


    public class SecurityContextSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        public SecurityContextSecurityTokenAuthenticator()
            : base()
        { }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return (token is SecurityContextSecurityToken);
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            SecurityContextSecurityToken sct = (SecurityContextSecurityToken)token;
            if (!IsTimeValid(sct))
            {
                this.ThrowExpiredContextFaultException(sct.ContextId, sct);
            }

            return sct.AuthorizationPolicies;
        }

        void ThrowExpiredContextFaultException(UniqueId contextId, SecurityContextSecurityToken sct)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new Exception(SR.Format(SR.SecurityContextExpired, contextId, sct.KeyGeneration == null ? "none" : sct.KeyGeneration.ToString())));
        }

        bool IsTimeValid(SecurityContextSecurityToken sct)
        {
            DateTime utcNow = DateTime.UtcNow;
            return (sct.ValidFrom <= utcNow && sct.ValidTo >= utcNow && sct.KeyEffectiveTime <= utcNow);
        }
   }
}
