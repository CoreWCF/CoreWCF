// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;
using MSamlTokens = Microsoft.IdentityModel.Tokens.Saml;
using Xunit;

namespace CoreWCF.Http.Tests.Security
{
    public class SamlSubjectKeyTests
    {
        private const string SamlNamespace = "urn:oasis:names:tc:SAML:1.0:assertion";
        private const string DsigNamespace = "http://www.w3.org/2000/09/xmldsig#";
        private const string WsseNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        private const string TestAudience = "urn:test:audience";
        private const string TestSubjectName = "user@example.com";

        [Theory]
        [InlineData("embedded")]
        [InlineData("ski")]
        [InlineData("str")]
        public void SubjectCertificateKey_IsIndependentOfIssuerKey(string keyInfoStyle)
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            using X509Certificate2 subject = CreateCertificate("Subject");
            string assertionXml = CreateSignedAssertion(issuer, new[] { CertificateKeyInfo(subject, keyInfoStyle) });
            SecurityTokenResolver resolver = CreateCertificateResolver(issuer, subject);

            SamlSecurityToken token = ReadAssertion(assertionXml, resolver);

            AssertCertificateKey(Assert.Single(token.SecurityKeys), subject, issuer);
        }

        [Fact]
        public void SubjectProofKey_VerifiesSubjectEndorsementNotIssuerSignature()
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            using X509Certificate2 subject = CreateCertificate("Subject");
            string assertionXml = CreateSignedAssertion(issuer, new[] { CertificateKeyInfo(subject, "embedded") });
            SamlSecurityToken token = ReadAssertion(assertionXml);
            X509AsymmetricSecurityKey key = Assert.IsType<X509AsymmetricSecurityKey>(Assert.Single(token.SecurityKeys));
            RSA proofKey = Assert.IsAssignableFrom<RSA>(
                key.GetAsymmetricAlgorithm(CoreWCF.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature, false));
            byte[] message = Encoding.UTF8.GetBytes("subject proof");
            using RSA subjectPrivateKey = subject.GetRSAPrivateKey();
            using RSA issuerPrivateKey = issuer.GetRSAPrivateKey();
            byte[] subjectSignature = subjectPrivateKey.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            byte[] issuerSignature = issuerPrivateKey.SignData(message, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            Assert.True(proofKey.VerifyData(message, subjectSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            Assert.False(proofKey.VerifyData(message, issuerSignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        [Theory]
        [InlineData("embedded")]
        [InlineData("ski")]
        [InlineData("str")]
        public void SubjectTokenResolution_UsesSubjectIdentifier(string keyInfoStyle)
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            using X509Certificate2 subject = CreateCertificate("Subject");
            string assertionXml = CreateSignedAssertion(issuer, new[] { CertificateKeyInfo(subject, keyInfoStyle) });
            RecordingTokenResolver resolver = new RecordingTokenResolver(CreateCertificateResolver(issuer, subject));

            ReadAssertion(assertionXml, resolver);

            Assert.Equal(new[] { issuer.Thumbprint, subject.Thumbprint },
                resolver.ResolvedTokens.Select(token => Assert.IsType<X509SecurityToken>(token).Certificate.Thumbprint));
        }

        [Theory]
        [InlineData("embedded")]
        [InlineData("ski")]
        [InlineData("str")]
        public async Task SubjectCertificatePolicy_UsesSubjectClaimSet(string keyInfoStyle)
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            using X509Certificate2 subject = CreateCertificate("Subject");
            string assertionXml = CreateSignedAssertion(issuer, new[] { CertificateKeyInfo(subject, keyInfoStyle) });
            SamlSecurityToken token = ReadAssertion(assertionXml, CreateCertificateResolver(issuer, subject));
            SamlSecurityTokenAuthenticator authenticator = CreateAuthenticator(issuer, subject);

            AuthorizationContext context = AuthorizationContext.CreateDefaultAuthorizationContext(
                await authenticator.ValidateTokenAsync(token));

            X509CertificateClaimSet subjectClaims = Assert.Single(context.ClaimSets.OfType<X509CertificateClaimSet>());
            Assert.Equal(subject.Thumbprint, subjectClaims.X509Certificate.Thumbprint);
            ClaimSet assertionClaims = Assert.Single(context.ClaimSets, claimSet => !(claimSet is X509CertificateClaimSet));
            X509CertificateClaimSet issuerClaims = Assert.IsType<X509CertificateClaimSet>(assertionClaims.Issuer);
            Assert.Equal(issuer.Thumbprint, issuerClaims.X509Certificate.Thumbprint);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void BearerWithoutSubjectKey_HasEmptySecurityKeys(int statementCount)
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            string assertionXml = CreateSignedAssertion(issuer, new string[statementCount]);

            SamlSecurityToken token = ReadAssertion(assertionXml);

            Assert.NotNull(token.SecurityKeys);
            Assert.Empty(token.SecurityKeys);
        }

        [Fact]
        public void MultipleStatements_CollectOnlySubjectKeys()
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            using X509Certificate2 firstSubject = CreateCertificate("FirstSubject");
            using X509Certificate2 lastSubject = CreateCertificate("LastSubject");
            string assertionXml = CreateSignedAssertion(issuer, new[]
            {
                CertificateKeyInfo(firstSubject, "embedded"),
                null,
                CertificateKeyInfo(lastSubject, "embedded")
            });

            SamlSecurityToken token = ReadAssertion(assertionXml);

            Assert.Collection(token.SecurityKeys,
                key => AssertCertificateKey(key, firstSubject, issuer),
                key => AssertCertificateKey(key, lastSubject, issuer));
        }

        [Theory]
        [InlineData("ski")]
        [InlineData("str")]
        [InlineData("key-name")]
        public void UnresolvableSubjectKey_IsRejected(string keyInfoStyle)
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            using X509Certificate2 subject = CreateCertificate("Subject");
            string keyInfo = keyInfoStyle == "key-name"
                ? $"<ds:KeyInfo xmlns:ds=\"{DsigNamespace}\"><ds:KeyName>unresolved-subject</ds:KeyName></ds:KeyInfo>"
                : CertificateKeyInfo(subject, keyInfoStyle);
            string assertionXml = CreateSignedAssertion(issuer, new[] { keyInfo });

            SecurityTokenException exception = Assert.Throws<SecurityTokenException>(
                () => ReadAssertion(assertionXml, CreateCertificateResolver(issuer)));

            Assert.Contains("SamlSubject", exception.Message);
            Assert.Contains("cannot be resolved", exception.Message);
        }

        [Fact]
        public async Task BearerKeyOnly_RejectsWrongAudience()
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            string assertionXml = CreateSignedAssertion(issuer, new string[1], audience: "urn:test:wrong-audience");
            SamlSecurityToken token = ReadAssertion(assertionXml);
            SamlSecurityTokenAuthenticator authenticator = CreateAuthenticator(issuer);
            authenticator.AudienceUriMode = AudienceUriMode.BearerKeyOnly;

            SecurityTokenException exception = await Assert.ThrowsAsync<SecurityTokenException>(
                async () => await authenticator.ValidateTokenAsync(token));

            Assert.Contains("audience", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task BearerKeyOnly_AcceptsValidAudience()
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            SamlSecurityToken token = ReadAssertion(CreateSignedAssertion(issuer, new string[1]));
            SamlSecurityTokenAuthenticator authenticator = CreateAuthenticator(issuer);
            authenticator.AudienceUriMode = AudienceUriMode.BearerKeyOnly;

            Assert.Single(await authenticator.ValidateTokenAsync(token));
        }

        [Theory]
        [InlineData("tampered-subject")]
        [InlineData("tampered-signature")]
        [InlineData("subject-signer")]
        public void InvalidIssuerSignature_IsRejected(string invalidSignature)
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            using X509Certificate2 subject = CreateCertificate("Subject");
            string assertionXml = CreateSignedAssertion(issuer, new[] { CertificateKeyInfo(subject, "embedded") },
                signingCertificate: invalidSignature == "subject-signer" ? subject : issuer);
            if (invalidSignature == "tampered-subject")
            {
                assertionXml = assertionXml.Replace(TestSubjectName, "tampered@example.com");
            }
            else if (invalidSignature == "tampered-signature")
            {
                XmlDocument document = new XmlDocument { PreserveWhitespace = true };
                document.LoadXml(assertionXml);
                XmlElement signatureValue = Assert.IsType<XmlElement>(
                    document.GetElementsByTagName("SignatureValue", DsigNamespace)[0]);
                byte[] signature = Convert.FromBase64String(signatureValue.InnerText);
                signature[0] ^= 1;
                signatureValue.InnerText = Convert.ToBase64String(signature);
                assertionXml = document.OuterXml;
            }

            Assert.Throws<Microsoft.IdentityModel.Xml.XmlValidationException>(() => ReadAssertion(assertionXml));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ReferencedSymmetricKeys_UseSubjectMaterialAndDeduplicate(bool distinctKeys)
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            byte[] firstKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
            byte[] secondKey = (byte[])firstKey.Clone();
            if (distinctKeys)
            {
                secondKey[0] ^= 1;
            }
            ReferencedKeyResolver resolver = new ReferencedKeyResolver(new[] { firstKey, secondKey });
            string assertionXml = CreateSignedAssertion(issuer, new[] { resolver.KeyInfo(0), resolver.KeyInfo(1) });

            SamlSecurityToken token = ReadAssertion(assertionXml, resolver);

            Assert.Equal(distinctKeys ? 2 : 1, token.SecurityKeys.Count);
            Assert.Equal(firstKey, Assert.IsAssignableFrom<SymmetricSecurityKey>(token.SecurityKeys[0]).GetSymmetricKey());
            if (distinctKeys)
            {
                Assert.Equal(secondKey, Assert.IsAssignableFrom<SymmetricSecurityKey>(token.SecurityKeys[1]).GetSymmetricKey());
            }
        }

        [Fact]
        public void ResolverBackedSubjectKey_WithoutToken_IsAccepted()
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            byte[] subjectKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
            ReferencedKeyResolver resolver = new ReferencedKeyResolver(new[] { subjectKey }, resolveTokens: false);
            string assertionXml = CreateSignedAssertion(issuer, new[] { resolver.KeyInfo(0) });

            SamlSecurityToken token = ReadAssertion(assertionXml, resolver);

            Assert.Equal(subjectKey, Assert.IsAssignableFrom<SymmetricSecurityKey>(Assert.Single(token.SecurityKeys)).GetSymmetricKey());
        }

        [Fact]
        public void InlineBinarySecret_IsUsedAsSubjectKey()
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            byte[] subjectKey = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
            string keyInfo = $"<ds:KeyInfo xmlns:ds=\"{DsigNamespace}\">" +
                "<trust:BinarySecret xmlns:trust=\"http://docs.oasis-open.org/ws-sx/ws-trust/200512\">" +
                Convert.ToBase64String(subjectKey) + "</trust:BinarySecret></ds:KeyInfo>";
            string assertionXml = CreateSignedAssertion(issuer, new[] { keyInfo });

            SamlSecurityToken token = ReadAssertion(assertionXml);

            Assert.Equal(subjectKey, Assert.IsAssignableFrom<SymmetricSecurityKey>(Assert.Single(token.SecurityKeys)).GetSymmetricKey());
        }

        [Fact]
        public async Task SubjectKeyInfo_DoesNotSupplyIssuerTrust()
        {
            using X509Certificate2 issuer = CreateCertificate("Issuer");
            using X509Certificate2 subject = CreateCertificate("Subject");
            string assertionXml = CreateSignedAssertion(issuer, new[] { CertificateKeyInfo(subject, "embedded") });
            SamlSecurityToken token = ReadAssertion(assertionXml);
            SamlSecurityTokenAuthenticator authenticator = CreateAuthenticator(subject);

            SecurityTokenValidationException exception = await Assert.ThrowsAsync<SecurityTokenValidationException>(
                async () => await authenticator.ValidateTokenAsync(token));

            Assert.Equal("Unexpected certificate.", exception.Message);
        }

        private static SamlSecurityToken ReadAssertion(string assertionXml, SecurityTokenResolver resolver = null)
        {
            using XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(
                Encoding.UTF8.GetBytes(assertionXml), XmlDictionaryReaderQuotas.Max);
            return new SamlSerializer().ReadToken(reader, CoreWCF.Security.WSSecurityTokenSerializer.DefaultInstance, resolver);
        }

        private static SecurityTokenResolver CreateCertificateResolver(params X509Certificate2[] certificates)
        {
            List<SecurityToken> tokens = certificates.Select(certificate => (SecurityToken)new X509SecurityToken(certificate)).ToList();
            return SecurityTokenResolver.CreateDefaultSecurityTokenResolver(tokens.AsReadOnly(), false);
        }

        private static SamlSecurityTokenAuthenticator CreateAuthenticator(params X509Certificate2[] certificates)
        {
            SamlSecurityTokenAuthenticator authenticator = new SamlSecurityTokenAuthenticator(
                new SecurityTokenAuthenticator[] { new X509SecurityTokenAuthenticator(new ExpectedCertificateValidator(certificates)) });
            authenticator.AllowedAudienceUris.Add(TestAudience);
            return authenticator;
        }

        private static void AssertCertificateKey(SecurityKey key, X509Certificate2 subject, X509Certificate2 issuer)
        {
            X509AsymmetricSecurityKey certificateKey = Assert.IsType<X509AsymmetricSecurityKey>(key);
            RSA actual = Assert.IsAssignableFrom<RSA>(
                certificateKey.GetAsymmetricAlgorithm(CoreWCF.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature, false));
            using RSA subjectRsa = subject.GetRSAPublicKey();
            using RSA issuerRsa = issuer.GetRSAPublicKey();
            Assert.Equal(subjectRsa.ExportParameters(false).Modulus, actual.ExportParameters(false).Modulus);
            Assert.False(issuerRsa.ExportParameters(false).Modulus.SequenceEqual(actual.ExportParameters(false).Modulus));
        }

        private static string CertificateKeyInfo(X509Certificate2 certificate, string keyInfoStyle)
        {
            string content;
            if (keyInfoStyle == "embedded")
            {
                content = "<ds:X509Data><ds:X509Certificate>" + Convert.ToBase64String(certificate.RawData) +
                    "</ds:X509Certificate></ds:X509Data>";
            }
            else
            {
                Assert.True(X509SubjectKeyIdentifierClause.TryCreateFrom(certificate, out X509SubjectKeyIdentifierClause clause));
                string identifier = Convert.ToBase64String(clause.GetX509SubjectKeyIdentifier());
                content = keyInfoStyle == "ski"
                    ? "<ds:X509Data><ds:X509SKI>" + identifier + "</ds:X509SKI></ds:X509Data>"
                    : SecurityTokenReference(identifier,
                        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509SubjectKeyIdentifier");
            }
            return $"<ds:KeyInfo xmlns:ds=\"{DsigNamespace}\">{content}</ds:KeyInfo>";
        }

        private static string SecurityTokenReference(string identifier, string valueType)
        {
            return $"<o:SecurityTokenReference xmlns:o=\"{WsseNamespace}\"><o:KeyIdentifier " +
                $"ValueType=\"{valueType}\" " +
                "EncodingType=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary\">" +
                identifier + "</o:KeyIdentifier></o:SecurityTokenReference>";
        }

        private static string CreateSignedAssertion(
            X509Certificate2 issuerCertificate, string[] subjectKeyInfos, string audience = TestAudience,
            X509Certificate2 signingCertificate = null)
        {
            DateTime now = DateTime.UtcNow;
            List<MSamlTokens.SamlStatement> statements = new List<MSamlTokens.SamlStatement>();
            foreach (string keyInfo in subjectKeyInfos)
            {
                MSamlTokens.SamlSubject subject = new MSamlTokens.SamlSubject(
                    "http://schemas.xmlsoap.org/claims/upn", null, TestSubjectName);
                subject.ConfirmationMethods.Add(keyInfo == null
                    ? "urn:oasis:names:tc:SAML:1.0:cm:bearer"
                    : "urn:oasis:names:tc:SAML:1.0:cm:holder-of-key");
                if (statements.Count % 3 == 0)
                {
                    statements.Add(new MSamlTokens.SamlAuthenticationStatement(
                        subject, "urn:oasis:names:tc:SAML:1.0:am:password", now, null, null, null));
                }
                else if (statements.Count % 3 == 1)
                {
                    statements.Add(new MSamlTokens.SamlAttributeStatement(subject, new[]
                    {
                        new MSamlTokens.SamlAttribute("http://schemas.xmlsoap.org/claims", "EmailAddress", new[] { TestSubjectName })
                    }));
                }
                else
                {
                    statements.Add(new MSamlTokens.SamlAuthorizationDecisionStatement(
                        subject, "urn:test:resource", "Permit",
                        new[] { new MSamlTokens.SamlAction("Read", new Uri("urn:test:action")) }));
                }
            }
            MSamlTokens.SamlAssertion assertion = new MSamlTokens.SamlAssertion(
                "_" + Guid.NewGuid().ToString("N"), "urn:test:issuer", now,
                new MSamlTokens.SamlConditions(now.AddMinutes(-1), now.AddHours(1), new[]
                {
                    new MSamlTokens.SamlAudienceRestrictionCondition(new Uri(audience))
                }), null, statements);
            XmlDocument document = new XmlDocument { PreserveWhitespace = true };
            document.LoadXml(new MSamlTokens.SamlSecurityTokenHandler().WriteToken(new MSamlTokens.SamlSecurityToken(assertion)));
            XmlNamespaceManager namespaces = new XmlNamespaceManager(document.NameTable);
            namespaces.AddNamespace("saml", SamlNamespace);
            namespaces.AddNamespace("ds", DsigNamespace);
            XmlNodeList subjects = document.SelectNodes("/saml:Assertion/*/saml:Subject", namespaces);
            Assert.Equal(subjectKeyInfos.Length, subjects.Count);
            for (int index = 0; index < subjectKeyInfos.Length; index++)
            {
                if (subjectKeyInfos[index] != null)
                {
                    XmlDocument keyInfoDocument = new XmlDocument();
                    keyInfoDocument.LoadXml(subjectKeyInfos[index]);
                    subjects[index].SelectSingleNode("saml:SubjectConfirmation", namespaces).AppendChild(
                        document.ImportNode(keyInfoDocument.DocumentElement, true));
                }
            }

            // Insert proof KeyInfo before signing; changing signed XML afterwards would mask resolution regressions.
            using RSA signingKey = (signingCertificate ?? issuerCertificate).GetRSAPrivateKey();
            SignedXml signedXml = new SamlSignedXml(document) { SigningKey = signingKey };
            signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
            signedXml.SignedInfo.SignatureMethod = SignedXml.XmlDsigRSASHA256Url;
            Reference reference = new Reference("#" + assertion.AssertionId) { DigestMethod = SignedXml.XmlDsigSHA256Url };
            reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
            reference.AddTransform(new XmlDsigExcC14NTransform());
            signedXml.AddReference(reference);
            signedXml.KeyInfo = new KeyInfo();
            signedXml.KeyInfo.AddClause(new KeyInfoX509Data(issuerCertificate));
            signedXml.ComputeSignature();
            document.DocumentElement.AppendChild(document.ImportNode(signedXml.GetXml(), true));

            Assert.Equal(Convert.ToBase64String(issuerCertificate.RawData),
                document.SelectSingleNode("/saml:Assertion/ds:Signature/ds:KeyInfo/ds:X509Data/ds:X509Certificate", namespaces).InnerText);
            for (int index = 0; index < subjects.Count; index++)
            {
                XmlNode keyInfo = subjects[index].SelectSingleNode("saml:SubjectConfirmation/ds:KeyInfo", namespaces);
                if (subjectKeyInfos[index] == null)
                {
                    Assert.Null(keyInfo);
                }
                else
                {
                    Assert.NotNull(keyInfo);
                    XmlDocument expected = new XmlDocument();
                    expected.LoadXml(subjectKeyInfos[index]);
                    Assert.Equal(expected.DocumentElement.InnerText, keyInfo.InnerText);
                }
            }
            return document.OuterXml;
        }

        private static X509Certificate2 CreateCertificate(string name)
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new CertificateRequest(
                "CN=SamlSubjectKeyTests" + name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
            using X509Certificate2 certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx), (string)null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }

        private sealed class SamlSignedXml : SignedXml
        {
            public SamlSignedXml(XmlDocument document) : base(document) { }

            public override XmlElement GetIdElement(XmlDocument document, string idValue) =>
                document.DocumentElement.GetAttribute("AssertionID") == idValue
                    ? document.DocumentElement
                    : base.GetIdElement(document, idValue);
        }

        private sealed class ExpectedCertificateValidator : X509CertificateValidator
        {
            private readonly HashSet<string> _thumbprints;

            public ExpectedCertificateValidator(IEnumerable<X509Certificate2> certificates)
            {
                _thumbprints = new HashSet<string>(certificates.Select(certificate => certificate.Thumbprint), StringComparer.OrdinalIgnoreCase);
            }

            public override void Validate(X509Certificate2 certificate)
            {
                if (!_thumbprints.Contains(certificate.Thumbprint))
                {
                    throw new SecurityTokenValidationException("Unexpected certificate.");
                }
            }
        }

        private sealed class RecordingTokenResolver : SecurityTokenResolver
        {
            private readonly SecurityTokenResolver _inner;

            public RecordingTokenResolver(SecurityTokenResolver inner) => _inner = inner;

            public List<SecurityToken> ResolvedTokens { get; } = new List<SecurityToken>();

            protected override bool TryResolveTokenCore(SecurityKeyIdentifier identifier, out SecurityToken token)
            {
                bool resolved = _inner.TryResolveToken(identifier, out token);
                if (resolved)
                {
                    ResolvedTokens.Add(token);
                }
                return resolved;
            }

            protected override bool TryResolveTokenCore(SecurityKeyIdentifierClause clause, out SecurityToken token)
            {
                bool resolved = _inner.TryResolveToken(clause, out token);
                if (resolved)
                {
                    ResolvedTokens.Add(token);
                }
                return resolved;
            }

            protected override bool TryResolveSecurityKeyCore(SecurityKeyIdentifierClause clause, out SecurityKey key) =>
                _inner.TryResolveSecurityKey(clause, out key);
        }

        private sealed class ReferencedKeyResolver : SecurityTokenResolver
        {
            private readonly BinarySecretSecurityToken[] _tokens;
            private readonly SecurityKeyIdentifierClause[] _identifiers;
            private readonly bool _resolveTokens;

            public ReferencedKeyResolver(byte[][] keys, bool resolveTokens = true)
            {
                _tokens = keys.Select(key => new BinarySecretSecurityToken(key)).ToArray();
                _identifiers = new SecurityKeyIdentifierClause[keys.Length];
                _resolveTokens = resolveTokens;
                for (int index = 0; index < keys.Length; index++)
                {
                    using XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(
                        Encoding.UTF8.GetBytes(ReferenceXml(index)), XmlDictionaryReaderQuotas.Max);
                    reader.MoveToContent();
                    _identifiers[index] = CoreWCF.Security.WSSecurityTokenSerializer.DefaultInstance.ReadKeyIdentifierClause(reader);
                    Assert.Equal("EncryptedKeyHashIdentifierClause", _identifiers[index].GetType().Name);
                }
            }

            public string KeyInfo(int index) => $"<ds:KeyInfo xmlns:ds=\"{DsigNamespace}\">{ReferenceXml(index)}</ds:KeyInfo>";

            private static string ReferenceXml(int index) => SecurityTokenReference(
                Convert.ToBase64String(Enumerable.Repeat((byte)(index + 1), 20).ToArray()),
                "http://docs.oasis-open.org/wss/oasis-wss-soap-message-security-1.1#EncryptedKeySHA1");

            protected override bool TryResolveSecurityKeyCore(SecurityKeyIdentifierClause clause, out SecurityKey key)
            {
                int index = Array.FindIndex(_identifiers, identifier => identifier.Matches(clause));
                key = index < 0 ? null : _tokens[index].SecurityKeys[0];
                return key != null;
            }

            protected override bool TryResolveTokenCore(SecurityKeyIdentifier identifier, out SecurityToken token)
            {
                foreach (SecurityKeyIdentifierClause clause in identifier)
                {
                    if (TryResolveTokenCore(clause, out token))
                    {
                        return true;
                    }
                }
                token = null;
                return false;
            }

            protected override bool TryResolveTokenCore(SecurityKeyIdentifierClause clause, out SecurityToken token)
            {
                int index = Array.FindIndex(_identifiers, identifier => identifier.Matches(clause));
                token = !_resolveTokens || index < 0 ? null : _tokens[index];
                return token != null;
            }
        }
    }
}
