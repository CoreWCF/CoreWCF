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
        private readonly bool _actAsInitiator;
        protected SessionDerivedKeySecurityTokenParameters(SessionDerivedKeySecurityTokenParameters other) : base(other)
        {
        }

        public SessionDerivedKeySecurityTokenParameters() : base()
        {
            InclusionMode = SecurityTokenInclusionMode.AlwaysToInitiator;
            RequireDerivedKeys = false;
        }

        protected internal override bool SupportsClientAuthentication => false;
        protected internal override bool SupportsServerAuthentication => false;
        protected internal override bool SupportsClientWindowsIdentity => false;

        protected internal override bool HasAsymmetricKey => false;

        protected override SecurityTokenParameters CloneCore()
        {
            return new SessionDerivedKeySecurityTokenParameters(this);
        }

        protected internal override SecurityKeyIdentifierClause CreateKeyIdentifierClause(SecurityToken token, SecurityTokenReferenceStyle referenceStyle)
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

        protected internal override bool MatchesKeyIdentifierClause(SecurityToken token, SecurityKeyIdentifierClause keyIdentifierClause, SecurityTokenReferenceStyle referenceStyle)
        {
            if (referenceStyle == SecurityTokenReferenceStyle.Internal)
            {
                if (!(keyIdentifierClause is LocalIdKeyIdentifierClause localClause))
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
