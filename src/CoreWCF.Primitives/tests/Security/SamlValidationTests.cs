// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using Microsoft.IdentityModel.Tokens;
using MSamlTokens = Microsoft.IdentityModel.Tokens.Saml;
using Xunit;

namespace CoreWCF.Http.Tests.Security
{
    public class SamlValidationTests
    {
        private const string TestAudience = "urn:test:audience";
        private const string TestIssuer = "urn:test:issuer";
        private const string OriginalSubjectName = "user@example.com";
        private const string ReplacementSubjectName = "other@example.com";

        [Fact]
        public void TestSamlTokenValidation()
        {
            using X509Certificate2 certificate = CreateTestCertificate();
            string assertionXml = CreateSignedAssertion(certificate);
            CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler handler = CreateConfiguredHandler(certificate);

            ReadOnlyCollection<ClaimsIdentity> identities = ValidateAssertion(handler, assertionXml);
            Assert.NotEmpty(identities);

            string rewritten = assertionXml.Replace(OriginalSubjectName, ReplacementSubjectName);
            Assert.NotEqual(assertionXml, rewritten);
            Assert.ThrowsAny<Exception>(() => ValidateAssertion(handler, rewritten));
        }

        private static ReadOnlyCollection<ClaimsIdentity> ValidateAssertion(
            CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler handler, string assertionXml)
        {
            using StringReader stringReader = new StringReader(assertionXml);
            using XmlReader xmlReader = XmlReader.Create(stringReader);
            xmlReader.MoveToContent();
            CoreWCF.IdentityModel.Tokens.SecurityToken token = handler.ReadToken(xmlReader);
            return handler.ValidateToken(token);
        }

        private static string CreateSignedAssertion(X509Certificate2 certificate)
        {
            DateTime now = DateTime.UtcNow;
            Microsoft.IdentityModel.Tokens.SigningCredentials signingCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(
                new X509SecurityKey(certificate),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256Signature,
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.Sha256Digest);

            MSamlTokens.SamlSubject subject = new MSamlTokens.SamlSubject(
                "http://schemas.xmlsoap.org/claims/upn",
                null,
                OriginalSubjectName);
            subject.ConfirmationMethods.Add("urn:oasis:names:tc:SAML:1.0:cm:bearer");

            MSamlTokens.SamlAttribute attribute = new MSamlTokens.SamlAttribute(
                "http://schemas.xmlsoap.org/claims",
                "EmailAddress",
                new[] { OriginalSubjectName });

            MSamlTokens.SamlAttributeStatement statement = new MSamlTokens.SamlAttributeStatement(
                subject, new[] { attribute });

            MSamlTokens.SamlAudienceRestrictionCondition audienceCondition =
                new MSamlTokens.SamlAudienceRestrictionCondition(new Uri(TestAudience));
            MSamlTokens.SamlConditions conditions = new MSamlTokens.SamlConditions(
                now.AddMinutes(-1),
                now.AddHours(1),
                new MSamlTokens.SamlCondition[] { audienceCondition });

            MSamlTokens.SamlAssertion assertion = new MSamlTokens.SamlAssertion(
                "_" + Guid.NewGuid().ToString("N"),
                TestIssuer,
                now,
                conditions,
                samlAdvice: null,
                samlStatements: new MSamlTokens.SamlStatement[] { statement })
            {
                SigningCredentials = signingCredentials
            };

            MSamlTokens.SamlSecurityToken samlToken = new MSamlTokens.SamlSecurityToken(assertion);
            MSamlTokens.SamlSecurityTokenHandler msHandler = new MSamlTokens.SamlSecurityTokenHandler();

            StringBuilder sb = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                msHandler.WriteToken(writer, samlToken);
            }
            return sb.ToString();
        }

        private static CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler CreateConfiguredHandler(X509Certificate2 certificate)
        {
            ConfigurationBasedIssuerNameRegistry registry = new ConfigurationBasedIssuerNameRegistry();
            registry.AddTrustedIssuer(certificate.Thumbprint, TestIssuer);

            List<CoreWCF.IdentityModel.Tokens.SecurityToken> tokens = new List<CoreWCF.IdentityModel.Tokens.SecurityToken>
            {
                new CoreWCF.IdentityModel.Tokens.X509SecurityToken(certificate)
            };
            CoreWCF.IdentityModel.Selectors.SecurityTokenResolver resolver =
                CoreWCF.IdentityModel.Selectors.SecurityTokenResolver.CreateDefaultSecurityTokenResolver(
                    tokens.AsReadOnly(), false);

            SecurityTokenHandlerConfiguration configuration = new SecurityTokenHandlerConfiguration
            {
                IssuerNameRegistry = registry,
                IssuerTokenResolver = resolver,
                CertificateValidator = X509CertificateValidator.None
            };
            configuration.AudienceRestriction.AllowedAudienceUris.Add(new Uri(TestAudience));

            CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler handler =
                new CoreWCF.IdentityModel.Tokens.SamlSecurityTokenHandler()
                {
                    Configuration = configuration
                };
            return handler;
        }

        private static X509Certificate2 CreateTestCertificate()
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new CertificateRequest(
                "CN=SamlValidationTests",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));

            DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            DateTimeOffset notAfter = DateTimeOffset.UtcNow.AddDays(1);

            using X509Certificate2 ephemeral = request.CreateSelfSigned(notBefore, notAfter);
            byte[] pfx = ephemeral.Export(X509ContentType.Pfx);
            return new X509Certificate2(
                pfx,
                (string)null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        }
    }
}
