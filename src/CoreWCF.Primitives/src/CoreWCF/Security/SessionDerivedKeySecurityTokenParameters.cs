// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class SessionDerivedKeySecurityTokenParameters : SecurityTokenParameters
    {
        bool actAsInitiator;
        protected SessionDerivedKeySecurityTokenParameters(SessionDerivedKeySecurityTokenParameters other) : base(other)
        {
            this.actAsInitiator = other.actAsInitiator;
        }

        public SessionDerivedKeySecurityTokenParameters(bool actAsInitiator) : base()
        {
            this.actAsInitiator = actAsInitiator;
            this.InclusionMode = actAsInitiator ? SecurityTokenInclusionMode.AlwaysToRecipient : SecurityTokenInclusionMode.AlwaysToInitiator;
            base.RequireDerivedKeys = false;
        }

        internal protected override bool SupportsClientAuthentication => false;
        internal protected override bool SupportsServerAuthentication => false;
        internal protected override bool SupportsClientWindowsIdentity => false;

        internal protected override bool HasAsymmetricKey => false;

        protected override SecurityTokenParameters CloneCore()
        {
            return new SessionDerivedKeySecurityTokenParameters(this);
        }

        internal protected override SecurityKeyIdentifierClause CreateKeyIdentifierClause(SecurityToken token, SecurityTokenReferenceStyle referenceStyle)
        {
            if (referenceStyle == SecurityTokenReferenceStyle.Internal)
            {
                return token.CreateKeyIdentifierClause<LocalIdKeyIdentifierClause>();
            }
            else
            {
                return null;
            }
        }

        internal protected override bool MatchesKeyIdentifierClause(SecurityToken token, SecurityKeyIdentifierClause keyIdentifierClause, SecurityTokenReferenceStyle referenceStyle)
        {
            if (referenceStyle == SecurityTokenReferenceStyle.Internal)
            {
                LocalIdKeyIdentifierClause localClause = keyIdentifierClause as LocalIdKeyIdentifierClause;
                if (localClause == null)
                {
                    return false;
                }
                else
                {
                    return (localClause.LocalId == token.Id);
                }
            }
            else
            {
                return false;
            }
        }

        protected internal override void InitializeSecurityTokenRequirement(SecurityTokenRequirement requirement)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException());
        }
    }
}
