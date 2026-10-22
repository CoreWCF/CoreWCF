// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using MSIdentityTokens = Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CoreWCF.Http.Tests.Security
{
    public class SamlIssuerValidationTests
    {
        private const string TestAudience = "urn:test:audience";
        private const string TestIssuer = "urn:test:issuer";
        private const string TestSubjectName = "user@example.com";

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RegisteredSigningCertificate_IsAccepted(bool saml2)
        {
            using X509Certificate2 certificate = CreateTestCertificate();
            ConfigurationBasedIssuerNameRegistry registry = new ConfigurationBasedIssuerNameRegistry();
            registry.AddTrustedIssuer(certificate.Thumbprint, TestIssuer);
            SecurityTokenHandler handler = CreateHandler(saml2, registry);
            SecurityToken token = ReadToken(handler, CreateSignedAssertion(saml2, certificate));

            ClaimsIdentity identity = Assert.Single(handler.ValidateToken(token));
            Claim subject = Assert.Single(identity.FindAll(ClaimTypes.NameIdentifier));
            Assert.Equal(TestSubjectName, subject.Value);
            Assert.Equal(TestIssuer, subject.Issuer);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UnregisteredSigningCertificate_IsRejected(bool saml2)
        {
            using X509Certificate2 registeredCertificate = CreateTestCertificate();
            using X509Certificate2 unregisteredCertificate = CreateTestCertificate();
            ConfigurationBasedIssuerNameRegistry registry = new ConfigurationBasedIssuerNameRegistry();
            registry.AddTrustedIssuer(registeredCertificate.Thumbprint, TestIssuer);
            SecurityTokenHandler handler = CreateHandler(saml2, registry);
            SecurityToken token = ReadToken(handler, CreateSignedAssertion(saml2, unregisteredCertificate));

            handler.Configuration.CertificateValidator.Validate(unregisteredCertificate);
            SecurityTokenException exception = Assert.Throws<SecurityTokenException>(() => handler.ValidateToken(token));
            Assert.Contains("ID4175", exception.Message);
        }

        [Theory]
        [InlineData(false, null)]
        [InlineData(true, null)]
        [InlineData(false, "")]
        [InlineData(true, "")]
        [InlineData(false, " ")]
        [InlineData(true, " ")]
        [InlineData(false, "\t\r\n")]
        [InlineData(true, "\t\r\n")]
        public void UnusableCustomIssuerName_IsRejected(bool saml2, string issuerName)
        {
            using X509Certificate2 certificate = CreateTestCertificate();
            IssuerNameRegistry registry = new TestIssuerNameRegistry((signingToken, requestedIssuerName) =>
            {
                Assert.Equal(certificate.Thumbprint, Assert.IsType<X509SecurityToken>(signingToken).Certificate.Thumbprint);
                Assert.Equal(TestIssuer, requestedIssuerName);
                return issuerName;
            });
            SecurityTokenHandler handler = CreateHandler(saml2, registry);
            SecurityToken token = ReadToken(handler, CreateSignedAssertion(saml2, certificate));

            SecurityTokenException exception = Assert.Throws<SecurityTokenException>(() => handler.ValidateToken(token));
            Assert.Contains("ID4175", exception.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CustomIssuerName_IsPreserved(bool saml2)
        {
            const string mappedIssuer = "urn:test:mapped-issuer";
            using X509Certificate2 certificate = CreateTestCertificate();
            IssuerNameRegistry registry = new TestIssuerNameRegistry((signingToken, requestedIssuerName) =>
            {
                Assert.Equal(certificate.Thumbprint, Assert.IsType<X509SecurityToken>(signingToken).Certificate.Thumbprint);
                Assert.Equal(TestIssuer, requestedIssuerName);
                return mappedIssuer;
            });
            SecurityTokenHandler handler = CreateHandler(saml2, registry);
            SecurityToken token = ReadToken(handler, CreateSignedAssertion(saml2, certificate));

            ClaimsIdentity identity = Assert.Single(handler.ValidateToken(token));
            Claim subject = Assert.Single(identity.FindAll(ClaimTypes.NameIdentifier));
            Assert.Equal(TestSubjectName, subject.Value);
            Assert.Equal(mappedIssuer, subject.Issuer);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CustomIssuerRegistryException_IsPreserved(bool saml2)
        {
            using X509Certificate2 certificate = CreateTestCertificate();
            SecurityTokenValidationException rejection = new SecurityTokenValidationException("Rejected by custom issuer policy.");
            IssuerNameRegistry registry = new TestIssuerNameRegistry((signingToken, requestedIssuerName) => throw rejection);
            SecurityTokenHandler handler = CreateHandler(saml2, registry);
            SecurityToken token = ReadToken(handler, CreateSignedAssertion(saml2, certificate));

            Assert.Same(rejection, Assert.Throws<SecurityTokenValidationException>(() => handler.ValidateToken(token)));
        }

        private static SecurityTokenHandler CreateHandler(bool saml2, IssuerNameRegistry registry)
        {
            SecurityTokenHandlerConfiguration configuration = new SecurityTokenHandlerConfiguration
            {
                IssuerNameRegistry = registry,
                CertificateValidator = X509CertificateValidator.None
            };
            configuration.AudienceRestriction.AllowedAudienceUris.Add(new Uri(TestAudience));

            // Resolve embedded X509Data rather than pinning the signing certificate in a resolver.
            Assert.Same(SecurityTokenHandlerConfiguration.DefaultIssuerTokenResolver, configuration.IssuerTokenResolver);
            SecurityTokenHandler handler = saml2 ? new Saml2SecurityTokenHandler() : new SamlSecurityTokenHandler();
            handler.Configuration = configuration;
            return handler;
        }

        private static SecurityToken ReadToken(SecurityTokenHandler handler, string assertionXml)
        {
            using StringReader stringReader = new StringReader(assertionXml);
            using XmlReader reader = XmlReader.Create(stringReader);
            reader.MoveToContent();
            return handler.ReadToken(reader);
        }

        private static string CreateSignedAssertion(bool saml2, X509Certificate2 certificate)
        {
            DateTime now = DateTime.UtcNow;
            MSIdentityTokens.SecurityTokenDescriptor descriptor = new MSIdentityTokens.SecurityTokenDescriptor
            {
                Issuer = TestIssuer,
                Audience = TestAudience,
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, TestSubjectName),
                    new Claim(ClaimTypes.Email, TestSubjectName)
                }),
                IssuedAt = now,
                NotBefore = now.AddMinutes(-1),
                Expires = now.AddHours(1),
                SigningCredentials = new MSIdentityTokens.SigningCredentials(
                    new MSIdentityTokens.X509SecurityKey(certificate),
                    MSIdentityTokens.SecurityAlgorithms.RsaSha256Signature,
                    MSIdentityTokens.SecurityAlgorithms.Sha256Digest)
            };
            MSIdentityTokens.SecurityTokenHandler handler = saml2
                ? new MSIdentityTokens.Saml2.Saml2SecurityTokenHandler()
                : new MSIdentityTokens.Saml.SamlSecurityTokenHandler();
            return handler.WriteToken(handler.CreateToken(descriptor));
        }

        private static X509Certificate2 CreateTestCertificate()
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new CertificateRequest(
                "CN=SamlIssuerValidationTests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
            using X509Certificate2 certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
            return new X509Certificate2(certificate.Export(X509ContentType.Pfx), (string)null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }

        private sealed class TestIssuerNameRegistry : IssuerNameRegistry
        {
            private readonly Func<SecurityToken, string, string> _getIssuerName;

            public TestIssuerNameRegistry(Func<SecurityToken, string, string> getIssuerName)
            {
                _getIssuerName = getIssuerName;
            }

            public override string GetIssuerName(SecurityToken securityToken) => throw new NotSupportedException();

            public override string GetIssuerName(SecurityToken securityToken, string requestedIssuerName) =>
                _getIssuerName(securityToken, requestedIssuerName);
        }
    }
}
