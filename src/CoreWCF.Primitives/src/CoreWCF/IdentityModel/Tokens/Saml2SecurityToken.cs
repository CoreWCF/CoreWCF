// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.Runtime;
using CoreWCF.Security;
using MSIdentitySAML2 = Microsoft.IdentityModel.Tokens.Saml2;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// A security token backed by a SAML2 assertion.
    /// </summary>
    public class Saml2SecurityToken : SecurityToken
    {
        private readonly ReadOnlyCollection<SecurityKey> _keys;

        protected Saml2SecurityToken()
        {
        }

        public Saml2SecurityToken(MSIdentitySAML2.Saml2SecurityToken saml2SecurityToken, ReadOnlyCollection<SecurityKey> keys)
        {
            Fx.Assert(saml2SecurityToken != null, "Saml2SecurityToken can't be null");
            WrappedSaml2SecurityToken = saml2SecurityToken;
            Assertion = WrappedSaml2SecurityToken.Assertion;
            _keys = keys;
        }

        public Saml2SecurityToken(MSIdentitySAML2.Saml2Assertion assertion)
        {
            Initialize(assertion);
        }

        protected void Initialize(MSIdentitySAML2.Saml2Assertion assertion)
        {
            Assertion = assertion ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(assertion));
        }

        internal string AssertionXML { get; set; }

        public override string Id => Assertion.Id.Value;

        internal MSIdentitySAML2.Saml2SecurityToken WrappedSaml2SecurityToken { get; }

        public MSIdentitySAML2.Saml2Assertion Assertion { get; private set; }

        internal SecurityToken SigningToken { get; set; }

        public override DateTime ValidFrom
        {
            get
            {
                if (Assertion.Conditions != null && Assertion.Conditions.NotBefore.HasValue)
                {
                    return Assertion.Conditions.NotBefore.Value;
                }

                return SecurityUtils.MinUtcDateTime;
            }
        }

        public override DateTime ValidTo
        {
            get
            {
                if (Assertion.Conditions != null && Assertion.Conditions.NotOnOrAfter.HasValue)
                {
                    return Assertion.Conditions.NotOnOrAfter.Value;
                }

                return SecurityUtils.MaxUtcDateTime;
            }
        }

        /// <summary>
        /// Gets the collection of <see cref="SecurityKey"/> contained in this token.
        /// </summary>
        public override ReadOnlyCollection<SecurityKey> SecurityKeys => _keys;
    }
}
