// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography.X509Certificates;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;
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
            SecurityKey verificationKey = SamlSerializer.ResolveSecurityKey(securityKeyIdentifier, outOfBandTokenResolver); ;
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
                SamlSubjectStatement samlSubject = (SamlSubjectStatement)stmt;
                SecurityKeyIdentifier keyIdentifier = null;
                SecurityToken securityToken = null;
                if (samlSubject.Subject.KeyInfo != null)
                {
                    keyIdentifier = CoreWCF.Security.SecurityUtils.CreateSecurityKeyIdentifier(samlSubject.Subject.KeyInfo);
                    securityToken = SamlSerializer.ResolveSecurityToken(securityKeyIdentifier, outOfBandTokenResolver);
                }
                InternalSamlSubjectStatement internalSamlSubjectStatement = new InternalSamlSubjectStatement(samlSubject, keyIdentifier, securityToken);
                samlSecurityToken.SamlStatements.Add(internalSamlSubjectStatement);
            }

            //do a final validation on sign.
            assertion.Signature.SignedInfo.Verify(Microsoft.IdentityModel.Tokens.CryptoProviderFactory.Default);
            return samlSecurityToken;
        }

        internal static SecurityKey ResolveSecurityKey(SecurityKeyIdentifier ski, SecurityTokenResolver tokenResolver)
        {
            if (ski == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(ski));

            if (tokenResolver != null)
            {
                for (int i = 0; i < ski.Count; ++i)
                {
                    if (tokenResolver.TryResolveSecurityKey(ski[i], out SecurityKey key))
                        return key;
                }
            }

            if (ski.CanCreateKey)
                return ski.CreateKey();

            return null;
        }

        internal static SecurityToken ResolveSecurityToken(SecurityKeyIdentifier ski, SecurityTokenResolver tokenResolver)
        {
            SecurityToken token = null;

            if (tokenResolver != null)
            {
                tokenResolver.TryResolveToken(ski, out token);
            }

            if (token == null)
            {
                // Check if this is a RSA key.
                if (ski.TryFind(out RsaKeyIdentifierClause rsaClause))
                    token = new RsaSecurityToken(rsaClause.Rsa);
            }

            if (token == null)
            {
                // Check if this is a X509RawDataKeyIdentifier Clause.
                if (ski.TryFind(out X509RawDataKeyIdentifierClause rawDataKeyIdentifierClause))
                    token = new X509SecurityToken(new X509Certificate2(rawDataKeyIdentifierClause.GetX509RawData()));
            }

            return token;
        }
    }
}
