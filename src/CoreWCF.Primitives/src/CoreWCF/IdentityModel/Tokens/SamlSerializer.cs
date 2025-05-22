// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.X509Certificates;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using MSSAmlSerializer = Microsoft.IdentityModel.Tokens.Saml.SamlSerializer;
using Microsoft.IdentityModel.Tokens.Saml;

namespace CoreWCF.IdentityModel.Tokens
{
    public class SamlSerializer
    {
        private readonly MSSAmlSerializer _mSSamlSerializer;
        public SamlSerializer()
        {
            _mSSamlSerializer = new MSSAmlSerializer();
        }

        public virtual SamlSecurityToken ReadToken(XmlDictionaryReader reader, SecurityTokenSerializer keyInfoSerializer, SecurityTokenResolver outOfBandTokenResolver)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            var assertion = _mSSamlSerializer.ReadAssertion(reader);
            if (assertion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SAMLUnableToLoadAssertion)));
            }

            SecurityKeyIdentifier securityKeyIdentifier = CoreWCF.Security.SecurityUtils.CreateSecurityKeyIdentifier(assertion.Signature.KeyInfo);
            SecurityKey verificationKey = SamlSerializer.ResolveSecurityKey(securityKeyIdentifier, outOfBandTokenResolver);
            if (verificationKey == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SAMLUnableToResolveSignatureKey, "issuer")));
            }

            var samlSecurityToken = new SamlSecurityToken(assertion)
            {
                SigningToken = SamlSerializer.ResolveSecurityToken(securityKeyIdentifier, outOfBandTokenResolver)
            };
            if (samlSecurityToken.SigningToken == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SamlSigningTokenNotFound)));
            }

            foreach (SamlStatement stmt in assertion.Statements)
            {
                // ...existing code...
            }
            // ...existing code...
            return samlSecurityToken;
        }

        // ...existing code...
    }
}
