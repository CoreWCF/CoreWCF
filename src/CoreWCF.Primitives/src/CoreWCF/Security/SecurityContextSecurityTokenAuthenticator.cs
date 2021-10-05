// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    public class SecurityContextSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        public SecurityContextSecurityTokenAuthenticator() : base()
        { }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return (token is SecurityContextSecurityToken);
        }

        protected override ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token)
        {
            SecurityContextSecurityToken sct = (SecurityContextSecurityToken)token;
            if (!IsTimeValid(sct))
            {
                ThrowExpiredContextFaultException(sct.ContextId, sct);
            }

            return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(sct.AuthorizationPolicies);
        }

        private void ThrowExpiredContextFaultException(UniqueId contextId, SecurityContextSecurityToken sct)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new Exception(SR.Format(SR.SecurityContextExpired, contextId, sct.KeyGeneration == null ? "none" : sct.KeyGeneration.ToString())));
        }

        private bool IsTimeValid(SecurityContextSecurityToken sct)
        {
            DateTime utcNow = DateTime.UtcNow;
            return (sct.ValidFrom <= utcNow && sct.ValidTo >= utcNow && sct.KeyEffectiveTime <= utcNow);
        }
    }
}
