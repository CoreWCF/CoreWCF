// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.Security;
using CoreWCF.Runtime;
using MSIdentitySAML = Microsoft.IdentityModel.Tokens.Saml;

namespace CoreWCF.IdentityModel.Tokens
{
    public class SamlSecurityToken : SecurityToken
    {
        private readonly ReadOnlyCollection<SecurityKey> _keys;
        protected SamlSecurityToken()
        {
        }

        internal SamlSecurityToken(MSIdentitySAML.SamlSecurityToken samlSecurityToken, ReadOnlyCollection<SecurityKey> keys)
        {
            Fx.Assert(samlSecurityToken != null, "SamlSecurityToken can't be null");
            WrappedSamlSecurityToken = samlSecurityToken;
            Assertion = WrappedSamlSecurityToken.Assertion;
            _keys = keys;
        }

        public SamlSecurityToken(MSIdentitySAML.SamlAssertion assertion)
        {
            Initialize(assertion);
        }

        protected void Initialize(MSIdentitySAML.SamlAssertion assertion)
        {
            if (assertion != null)
            {
                Assertion = assertion;
                // this.assertion.MakeReadOnly();
                SamlStatements = new List<InternalSamlSubjectStatement>();
            }
            else
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(assertion));
        }

        public override string Id => Assertion.AssertionId;

        internal MSIdentitySAML.SamlSecurityToken WrappedSamlSecurityToken { get; }

        internal MSIdentitySAML.SamlAssertion Assertion { get; private set; }

        internal SecurityToken SigningToken { get; set; }

        internal List<InternalSamlSubjectStatement> SamlStatements { get; set; }

        public override DateTime ValidFrom
        {
            get
            {
                if (Assertion.Conditions != null)
                {
                    return Assertion.Conditions.NotBefore;
                }

                return SecurityUtils.MinUtcDateTime;
            }
        }

        public override DateTime ValidTo
        {
            get
            {
                if (Assertion.Conditions != null)
                {
                    return Assertion.Conditions.NotOnOrAfter;
                }

                return SecurityUtils.MaxUtcDateTime;
            }
        }

        public override ReadOnlyCollection<SecurityKey> SecurityKeys => _keys;

        public string AssertionXML { get; internal set; }

    }
}
