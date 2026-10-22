// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using MSIdentityTokens = Microsoft.IdentityModel.Tokens;
using MSamlTokens = Microsoft.IdentityModel.Tokens.Saml;
using MSXml = Microsoft.IdentityModel.Xml;
using Xunit;

namespace CoreWCF.Http.Tests.Security
{
    public class SamlSubjectConfirmationValidationTests
    {
        private const string TestAudience = "urn:test:audience";
        private const string TestIssuer = "urn:test:issuer";
        private const string TestSubjectName = "user@example.com";
        private const string Bearer = "urn:oasis:names:tc:SAML:1.0:cm:bearer";
        private const string SenderVouches = "urn:oasis:names:tc:SAML:1.0:cm:sender-vouches";
        private const string HolderOfKey = "urn:oasis:names:tc:SAML:1.0:cm:holder-of-key";
        private const string UnsupportedMethod = "urn:test:unsupported-confirmation";
        private const string SamlNamespace = "urn:oasis:names:tc:SAML:1.0:assertion";
        private const int SubjectStatementCount = 3;

        public static TheoryData<bool, int, bool> SubjectStatementCases
        {
            get
            {
                TheoryData<bool, int, bool> cases = new TheoryData<bool, int, bool>();
                foreach (bool federated in new[] { false, true })
                {
                    for (int statementIndex = 0; statementIndex < SubjectStatementCount; statementIndex++)
                    {
                        cases.Add(federated, statementIndex, false);
                        cases.Add(federated, statementIndex, true);
                    }
                }
                return cases;
            }
        }

        public static TheoryData<bool, string[], bool> ValidSubjectConfirmationCases
        {
            get
            {
                TheoryData<bool, string[], bool> cases = new TheoryData<bool, string[], bool>();
                foreach (bool federated in new[] { false, true })
                {
                    cases.Add(federated, Array.Empty<string>(), false);
                    cases.Add(federated, new[] { Bearer }, false);
                    cases.Add(federated, new[] { SenderVouches }, false);
                    cases.Add(federated, new[] { Bearer, SenderVouches }, false);
                    cases.Add(federated, new[] { HolderOfKey }, true);
                    cases.Add(federated, new[] { Bearer, HolderOfKey }, true);
                    cases.Add(federated, new[] { HolderOfKey, SenderVouches }, true);
                }
                return cases;
            }
        }

        [Theory]
        [MemberData(nameof(SubjectStatementCases))]
        public async Task UnsupportedConfirmationMethod_IsRejected(bool federated, int statementIndex, bool includeBearer)
        {
            using X509Certificate2 certificate = CreateTestCertificate();
            string[] methods = includeBearer ? new[] { Bearer, UnsupportedMethod } : new[] { UnsupportedMethod };
            string assertionXml = CreateSignedAssertion(certificate, methods, statementIndex: statementIndex);

            SecurityTokenValidationException exception = await Assert.ThrowsAsync<SecurityTokenValidationException>(
                () => ValidateTokenAsync(assertionXml, certificate, federated));
            Assert.Contains("unrecognized confirmation method", exception.Message);
            Assert.Contains(UnsupportedMethod, exception.Message);
        }

        [Theory]
        [MemberData(nameof(SubjectStatementCases))]
        public async Task HolderOfKeyWithoutSubjectKeyInfo_IsRejected(bool federated, int statementIndex, bool includeBearer)
        {
            using X509Certificate2 certificate = CreateTestCertificate();
            string[] methods = includeBearer ? new[] { Bearer, HolderOfKey } : new[] { HolderOfKey };
            string assertionXml = CreateSignedAssertion(certificate, methods, statementIndex: statementIndex);

            SecurityTokenValidationException exception = await Assert.ThrowsAsync<SecurityTokenValidationException>(
                () => ValidateTokenAsync(assertionXml, certificate, federated));
            Assert.Contains("holder-of-key", exception.Message);
            Assert.Contains("KeyInfo", exception.Message);
        }

        [Theory]
        [MemberData(nameof(ValidSubjectConfirmationCases))]
        public async Task ValidSubjectConfirmations_AreAccepted(bool federated, string[] methods, bool includeKeyInfo)
        {
            using X509Certificate2 certificate = CreateTestCertificate();
            string assertionXml = CreateSignedAssertion(certificate, methods, includeKeyInfo);

            await ValidateTokenAsync(assertionXml, certificate, federated);
        }

        [Fact]
        public async Task NonFederatedAudienceRestriction_IsStillEnforced()
        {
            using X509Certificate2 certificate = CreateTestCertificate();
            string assertionXml = CreateSignedAssertion(certificate, new[] { Bearer }, audience: "urn:test:wrong-audience");

            SecurityTokenException exception = await Assert.ThrowsAsync<SecurityTokenException>(
                () => ValidateTokenAsync(assertionXml, certificate, federated: false));
            Assert.Contains("audience", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task NonFederatedSigningCertificateTrust_IsStillEnforced()
        {
            using X509Certificate2 trustedCertificate = CreateTestCertificate();
            using X509Certificate2 untrustedCertificate = CreateTestCertificate();
            string assertionXml = CreateSignedAssertion(untrustedCertificate, new[] { Bearer });

            SecurityTokenValidationException exception = await Assert.ThrowsAsync<SecurityTokenValidationException>(
                () => ValidateTokenAsync(assertionXml, trustedCertificate, federated: false));
            Assert.Equal("Unexpected signing certificate.", exception.Message);
        }

        private static async Task ValidateTokenAsync(string assertionXml, X509Certificate2 trustedCertificate, bool federated)
        {
            X509CertificateValidator certificateValidator = new ExpectedSigningCertificateValidator(trustedCertificate);
            if (federated)
            {
                ConfigurationBasedIssuerNameRegistry registry = new ConfigurationBasedIssuerNameRegistry();
                registry.AddTrustedIssuer(trustedCertificate.Thumbprint, TestIssuer);
                SamlSecurityTokenHandler handler = new SamlSecurityTokenHandler
                {
                    Configuration = new SecurityTokenHandlerConfiguration
                    {
                        IssuerNameRegistry = registry,
                        CertificateValidator = certificateValidator
                    }
                };
                handler.Configuration.AudienceRestriction.AllowedAudienceUris.Add(new Uri(TestAudience));

                using StringReader stringReader = new StringReader(assertionXml);
                using XmlReader reader = XmlReader.Create(stringReader);
                reader.MoveToContent();
                SecurityToken token = handler.ReadToken(reader);
                Assert.NotEmpty(handler.ValidateToken(token));
            }
            else
            {
                SamlSecurityTokenAuthenticator authenticator = new SamlSecurityTokenAuthenticator(
                    new SecurityTokenAuthenticator[] { new X509SecurityTokenAuthenticator(certificateValidator) })
                {
                    AudienceUriMode = AudienceUriMode.Always
                };
                authenticator.AllowedAudienceUris.Add(TestAudience);

                SamlSerializer serializer = new SamlSerializer();
                using XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(
                    Encoding.UTF8.GetBytes(assertionXml), XmlDictionaryReaderQuotas.Max);
                SamlSecurityToken token = serializer.ReadToken(
                    reader, CoreWCF.Security.WSSecurityTokenSerializer.DefaultInstance, SecurityTokenHandlerConfiguration.DefaultIssuerTokenResolver);
                Assert.Equal(SubjectStatementCount, (await authenticator.ValidateTokenAsync(token)).Count);
            }
        }

        private static string CreateSignedAssertion(
            X509Certificate2 certificate, IEnumerable<string> confirmationMethods, bool includeKeyInfo = false,
            int statementIndex = -1, string audience = TestAudience)
        {
            DateTime now = DateTime.UtcNow;
            List<MSamlTokens.SamlSubject> subjects = new List<MSamlTokens.SamlSubject>();
            for (int index = 0; index < SubjectStatementCount; index++)
            {
                MSamlTokens.SamlSubject subject = new MSamlTokens.SamlSubject(
                    "http://schemas.xmlsoap.org/claims/upn", null, TestSubjectName);
                bool selectedSubject = statementIndex < 0 || statementIndex == index;
                foreach (string method in selectedSubject ? confirmationMethods : new[] { Bearer })
                {
                    subject.ConfirmationMethods.Add(method);
                }
                if (selectedSubject && includeKeyInfo)
                {
                    MSXml.X509Data x509Data = new MSXml.X509Data();
                    x509Data.Certificates.Add(Convert.ToBase64String(certificate.RawData));
                    subject.KeyInfo = new MSXml.KeyInfo();
                    subject.KeyInfo.X509Data.Add(x509Data);
                }
                subjects.Add(subject);
            }

            MSamlTokens.SamlStatement[] statements = new MSamlTokens.SamlStatement[]
            {
                new MSamlTokens.SamlAuthenticationStatement(subjects[0],
                    "urn:oasis:names:tc:SAML:1.0:am:password", now, null, null, null),
                new MSamlTokens.SamlAttributeStatement(subjects[1], new[]
                {
                    new MSamlTokens.SamlAttribute("http://schemas.xmlsoap.org/claims", "EmailAddress", new[] { TestSubjectName })
                }),
                new MSamlTokens.SamlAuthorizationDecisionStatement(subjects[2], "urn:test:resource", "Permit",
                    new[] { new MSamlTokens.SamlAction("Read", new Uri("urn:test:action")) })
            };
            MSamlTokens.SamlConditions conditions = new MSamlTokens.SamlConditions(
                now.AddMinutes(-1), now.AddHours(1), new MSamlTokens.SamlCondition[]
                {
                    new MSamlTokens.SamlAudienceRestrictionCondition(new Uri(audience))
                });
            MSamlTokens.SamlAssertion assertion = new MSamlTokens.SamlAssertion(
                "_" + Guid.NewGuid().ToString("N"), TestIssuer, now, conditions, null, statements)
            {
                SigningCredentials = new MSIdentityTokens.SigningCredentials(
                    new MSIdentityTokens.X509SecurityKey(certificate),
                    MSIdentityTokens.SecurityAlgorithms.RsaSha256Signature,
                    MSIdentityTokens.SecurityAlgorithms.Sha256Digest)
            };
            MSamlTokens.SamlSecurityTokenHandler handler = new MSamlTokens.SamlSecurityTokenHandler
            {
                Serializer = new SubjectKeyInfoSerializer()
            };
            string assertionXml = handler.WriteToken(new MSamlTokens.SamlSecurityToken(assertion));
            XmlDocument document = new XmlDocument();
            document.LoadXml(assertionXml);
            XmlNamespaceManager namespaces = new XmlNamespaceManager(document.NameTable);
            namespaces.AddNamespace("saml", SamlNamespace);
            namespaces.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            Assert.Equal(includeKeyInfo ? (statementIndex < 0 ? SubjectStatementCount : 1) : 0,
                document.SelectNodes("//saml:Subject/saml:SubjectConfirmation/ds:KeyInfo", namespaces).Count);
            return assertionXml;
        }

        private static X509Certificate2 CreateTestCertificate()
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new CertificateRequest(
                "CN=SamlSubjectConfirmationValidationTests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
            using X509Certificate2 certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx), (string)null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }

        private sealed class SubjectKeyInfoSerializer : MSamlTokens.SamlSerializer
        {
            protected override void WriteSubject(XmlWriter writer, MSamlTokens.SamlSubject subject)
            {
                if (subject.KeyInfo == null)
                {
                    base.WriteSubject(writer, subject);
                    return;
                }

                // The default writer omits subject KeyInfo, so emit it before the assertion is signed.
                writer.WriteStartElement(Prefix, "Subject", SamlNamespace);
                writer.WriteStartElement(Prefix, "NameIdentifier", SamlNamespace);
                writer.WriteAttributeString("Format", subject.NameFormat);
                writer.WriteString(subject.Name);
                writer.WriteEndElement();
                writer.WriteStartElement(Prefix, "SubjectConfirmation", SamlNamespace);
                foreach (string method in subject.ConfirmationMethods)
                {
                    writer.WriteElementString(Prefix, "ConfirmationMethod", SamlNamespace, method);
                }
                DSigSerializer.WriteKeyInfo(writer, subject.KeyInfo);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
        }

        private sealed class ExpectedSigningCertificateValidator : X509CertificateValidator
        {
            private readonly string _thumbprint;

            public ExpectedSigningCertificateValidator(X509Certificate2 certificate)
            {
                _thumbprint = certificate.Thumbprint;
            }

            public override void Validate(X509Certificate2 certificate)
            {
                if (!string.Equals(_thumbprint, certificate.Thumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    throw new SecurityTokenValidationException("Unexpected signing certificate.");
                }
            }
        }
    }
}
