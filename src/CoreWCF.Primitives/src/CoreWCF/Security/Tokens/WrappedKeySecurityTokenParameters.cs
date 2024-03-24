// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    internal class WrappedKeySecurityTokenParameters : SecurityTokenParameters
    {
        protected WrappedKeySecurityTokenParameters(WrappedKeySecurityTokenParameters other)
            : base(other)
        {
            // empty
        }

        public WrappedKeySecurityTokenParameters()
            : base()
        {
            InclusionMode = SecurityTokenInclusionMode.Once;
        }

        internal protected override bool HasAsymmetricKey { get { return false; } }
        internal protected override bool SupportsClientAuthentication { get { return false; } }
        internal protected override bool SupportsServerAuthentication { get { return true; } }
        internal protected override bool SupportsClientWindowsIdentity { get { return false; } }

        protected override SecurityTokenParameters CloneCore()
        {
            return new WrappedKeySecurityTokenParameters(this);
        }

        internal protected override SecurityKeyIdentifierClause CreateKeyIdentifierClause(SecurityToken token, SecurityTokenReferenceStyle referenceStyle)
        {
            return base.CreateKeyIdentifierClause<EncryptedKeyHashIdentifierClause, LocalIdKeyIdentifierClause>(token, referenceStyle);
        }

        protected internal override void InitializeSecurityTokenRequirement(SecurityTokenRequirement requirement)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }
    }
}
