// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using Microsoft.IdentityModel.Tokens.Saml;
using MSSAmlSerializer = Microsoft.IdentityModel.Tokens.Saml.SamlSerializer;

namespace CoreWCF.IdentityModel.Tokens
{
    public class SamlSerializer
    {
        private readonly MSSAmlSerializer _mSSamlSerializer;

        public SamlSerializer()
        {
            _mSSamlSerializer = new MSSAmlSerializer();
            _mSSamlSerializer.DSigSerializer = new Saml.DSigSerializerExtended();
        }

        public virtual SamlSecurityToken ReadToken(XmlDictionaryReader reader, SecurityTokenSerializer keyInfoSerializer, SecurityTokenResolver outOfBandTokenResolver)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            if (keyInfoSerializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyInfoSerializer));
            }

            SecurityToken? signingToken = null;
            SecurityKey? verificationKey = null;
            var assertion = LoadAssertion(reader, keyInfoSerializer, outOfBandTokenResolver, out signingToken, out verificationKey);

            if (assertion == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.SAMLUnableToLoadAssertion)));
            }

            var imSamlSecurityToken = new Microsoft.IdentityModel.Tokens.Saml.SamlSecurityToken(assertion);
            var samlSecurityToken = new SamlSecurityToken(imSamlSecurityToken, new ReadOnlyCollection<SecurityKey?>(new List<SecurityKey?>(new[] { verificationKey })));
            samlSecurityToken.SigningToken = signingToken;
            SecurityKeyIdentifier securityKeyIdentifier = CreateSecurityKeyIdentifier(assertion.Signature.KeyInfo, keyInfoSerializer);

            foreach (var stmt in assertion.Statements)
            {
                SamlSubjectStatement samlSubject = (SamlSubjectStatement)stmt;
                SecurityKeyIdentifier keyIdentifier = null;
                SecurityToken securityToken = null;
                if (samlSubject.Subject.KeyInfo != null)
                {
                    keyIdentifier = CreateSecurityKeyIdentifier(samlSubject.Subject.KeyInfo, keyInfoSerializer);
                    securityToken = ResolveSecurityToken(securityKeyIdentifier, outOfBandTokenResolver);
                }
                var internalSamlSubjectStatement = new InternalSamlSubjectStatement(samlSubject, keyIdentifier, securityToken);
                samlSecurityToken.SamlStatements.Add(internalSamlSubjectStatement);
            }

            //do a final validation on signature.
            if (signingToken is X509SecurityToken x509signingToken)
            {
                Microsoft.IdentityModel.Tokens.X509SecurityKey? imX509SecurityKey = null;
                var cryptoProviderFactory = new CoreWCF.Security.Sha1CryptoProviderFactory();
                try
                {
                    imX509SecurityKey = new Microsoft.IdentityModel.Tokens.X509SecurityKey(x509signingToken.Certificate);
                    imX509SecurityKey.CryptoProviderFactory = cryptoProviderFactory;
                    assertion.Signature.Verify(imX509SecurityKey, cryptoProviderFactory);
                }
                catch (Microsoft.IdentityModel.Xml.XmlValidationException xve)
                {
                    if (xve.Message.Contains("SignatureMethod is not supported"))
                    {
                        if (imX509SecurityKey?.PublicKey is null)
                        {
                            throw new SecurityTokenException(SR.Format(SR.PublicKeyNotFoundInX509SecurityKey, assertion.Signature.SignedInfo.SignatureMethod), xve);
                        }

                        throw new SecurityTokenException(SR.Format(SR.SignatureMethodNotSupported, assertion.Signature.SignedInfo.SignatureMethod), xve);
                    }

                    throw;
                }
            }
            else
            {
                assertion.Signature.SignedInfo.Verify(Microsoft.IdentityModel.Tokens.CryptoProviderFactory.Default);
            }

            return samlSecurityToken;
        }

        private SamlAssertion LoadAssertion(XmlDictionaryReader reader, SecurityTokenSerializer keyInfoSerializer, SecurityTokenResolver outOfBandTokenResolver, out SecurityToken signingToken, out SecurityKey? verificationKey)
        {
            if (reader == null)
                throw new ArgumentNullException(nameof(reader));

            SamlAssertion assertion = _mSSamlSerializer.ReadAssertion(reader);
            SecurityKeyIdentifier securityKeyIdentifier = CreateSecurityKeyIdentifier(assertion.Signature.KeyInfo, keyInfoSerializer);
            CoreWCF.IdentityModel.SecurityKeyIdentifierClause? securityKeyIdentifierClause = null;
            verificationKey = null;
            signingToken = null;
            if (securityKeyIdentifier.Count < 2 /*|| LocalAppContextSwitches.ProcessMultipleSecurityKeyIdentifierClauses*/)
            {
                verificationKey = SamlSerializer.ResolveSecurityKey(securityKeyIdentifier, outOfBandTokenResolver);
            }
            else
            {
                verificationKey = ResolveSecurityKey(securityKeyIdentifier, outOfBandTokenResolver, out securityKeyIdentifierClause);
            }

            if (verificationKey == null)
            {
                throw new SecurityTokenException(SR.Format(SR.SAMLUnableToResolveSignatureKey, assertion.Issuer));
            }

            if (securityKeyIdentifier.Count < 2 /*|| LocalAppContextSwitches.ProcessMultipleSecurityKeyIdentifierClauses*/)
            {
                signingToken = SamlSerializer.ResolveSecurityToken(securityKeyIdentifier, outOfBandTokenResolver);
            }
            else
            {
                signingToken = SamlSerializer.ResolveSecurityToken(new SecurityKeyIdentifier(securityKeyIdentifierClause), outOfBandTokenResolver);
            }

            if (signingToken == null)
            {
                throw new SecurityTokenException(SR.SamlSigningTokenNotFound);
            }

            return assertion;
        }

        private static SecurityKey ResolveSecurityKey(SecurityKeyIdentifier ski, SecurityTokenResolver tokenResolver, out SecurityKeyIdentifierClause clause)
        {
            if (ski == null)
                throw new ArgumentNullException(nameof(ski));

            clause = null;

            if (tokenResolver != null)
            {
                for (int i = 0; i < ski.Count; ++i)
                {
                    SecurityKey? key = null;
                    if (tokenResolver.TryResolveSecurityKey(ski[i], out key))
                    {
                        clause = ski[i];
                        return key;
                    }
                }
            }

            if (ski.CanCreateKey)
            {
                foreach (var skiClause in ski)
                {
                    if (skiClause.CanCreateKey)
                    {
                        clause = skiClause;
                        return clause.CreateKey();
                    }
                }

                throw new InvalidOperationException(SR.KeyIdentifierCannotCreateKey);
            }

            return null;
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

        internal static SecurityKeyIdentifier CreateSecurityKeyIdentifier(Microsoft.IdentityModel.Xml.KeyInfo keyInfo, SecurityTokenSerializer keyInfoSerializer)
        {
            var ski = new SecurityKeyIdentifier();
            if (keyInfo == null)
            {
                return ski;
            }

            if (keyInfo.RSAKeyValue != null)
            {
                throw new InvalidOperationException(SR.SamlRSANotSupported);
            }

            foreach (var objdata in keyInfo.X509Data)
            {
                foreach (string certificateStr in objdata.Certificates)
                {
                    byte[] data = Convert.FromBase64String(certificateStr);
                    ski.Add(new X509RawDataKeyIdentifierClause(data));
                }

                if (objdata.IssuerSerial != null)
                {
                    ski.Add(new X509IssuerSerialKeyIdentifierClause(objdata.IssuerSerial.IssuerName, objdata.IssuerSerial.SerialNumber));
                }

                if (!string.IsNullOrWhiteSpace(objdata.SKI))
                {
                    byte[] data = Convert.FromBase64String(objdata.SKI);
                    ski.Add(new X509SubjectKeyIdentifierClause(data));
                }
            }

            if (keyInfo is Saml.KeyInfo coreWcfKeyInfo)
            {
                if (coreWcfKeyInfo.SecurityTokenReference != null)
                {
                    var strIdentifierClause = CreateStrSecurityKeyIdentifier(coreWcfKeyInfo.SecurityTokenReference, keyInfoSerializer);
                    if (strIdentifierClause != null)
                    {
                        ski.Add(strIdentifierClause);
                    }
                }
            }

            return ski;
        }

        private static SecurityKeyIdentifierClause? CreateStrSecurityKeyIdentifier(Saml.SecurityTokenReference securityTokenReference, SecurityTokenSerializer keyInfoSerializer)
        {
            var ski = securityTokenReference.SecurityKeyIdentifier;
            var strString = $"""
                <o:SecurityTokenReference xmlns:o="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
                  <o:KeyIdentifier ValueType="{ski.ValueType}" EncodingType="{ski.EncodingType}">{ski.Value}</o:KeyIdentifier>
                </o:SecurityTokenReference>
                """;

            var dictReader = XmlDictionaryReader.CreateTextReader(Encoding.UTF8.GetBytes(strString), XmlDictionaryReaderQuotas.Max);
            dictReader.MoveToContent();
            if (!keyInfoSerializer.CanReadKeyIdentifierClause(dictReader))
            {
                return null;
            }

            return keyInfoSerializer.ReadKeyIdentifierClause(dictReader);
        }

    }
}
