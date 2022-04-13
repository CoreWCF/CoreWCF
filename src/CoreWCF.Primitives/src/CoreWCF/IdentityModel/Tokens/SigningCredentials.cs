// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.IdentityModel.Tokens
{
    public class SigningCredentials
    {
        private string _signatureAlgorithm;
        private SecurityKeyIdentifier _signingKeyIdentifier;

        public SigningCredentials(SecurityKey signingKey, string signatureAlgorithm, string digestAlgorithm) :
            this(signingKey, signatureAlgorithm, digestAlgorithm, null)
        { }

        public SigningCredentials(SecurityKey signingKey, string signatureAlgorithm, string digestAlgorithm, SecurityKeyIdentifier signingKeyIdentifier)
        {
            if (signingKey == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(signingKey)));
            }

            if (signatureAlgorithm == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(signatureAlgorithm)));
            }
            if (digestAlgorithm == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(digestAlgorithm)));
            }
            SigningKey = signingKey;
            _signatureAlgorithm = signatureAlgorithm;
            DigestAlgorithm = digestAlgorithm;
            _signingKeyIdentifier = signingKeyIdentifier;
        }

        public string DigestAlgorithm { get; }

        public string SignatureAlgorithm
        {
            get { return _signatureAlgorithm; }
        }

        public SecurityKey SigningKey { get; }

        public SecurityKeyIdentifier SigningKeyIdentifier
        {
            get { return _signingKeyIdentifier; }
        }
    }
}
