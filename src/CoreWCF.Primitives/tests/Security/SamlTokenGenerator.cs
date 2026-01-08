// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// This code runs on .NET Framework and was used to generate the SAML token
// used by the SAML serializer tests.

#if ENABLED
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;
using System.Text;
using System.Xml;

namespace SamlTokenGeneration
{
    public class SamlTokenGenerator
    {
        public void GenerateSamlAssertion()
        {
            // Create a unique ID for the SAML assertion
            string assertionId = "uuid-" + Guid.NewGuid().ToString();

            var samlSubject = CreateSamlSubject();

            // Create the authentication statement
            SamlAuthenticationStatement authStatement = new SamlAuthenticationStatement(
                samlSubject,
                "urn:oasis:names:tc:SAML:1.0:am:password", // Authentication method
                DateTime.UtcNow, // Authentication instant
                null, null, null
            );

            var attributes = new List<SamlAttribute>
            {
                // Add attributes
                CreateSamlAttribute("EmailAddress",
                "http://schemas.xmlsoap.org/claims", "user@corewcf.net"),
                CreateSamlAttribute("RequestorDomain",
                "http://schemas.microsoft.com/ws/2006/04/identity/claims", "example.com"),
                CreateSamlAttribute("action",
                "http://schemas.xmlsoap.org/ws/2006/12/authorization/claims", "FooService.ShowGroups"),
                CreateSamlAttribute("ThirdPartyRequested",
                "http://schemas.microsoft.com/ws/2006/04/identity/claims", "True")
            };

            // Create the attribute statement
            SamlAttributeStatement attrStatement = new SamlAttributeStatement(
                new SamlSubject(
                    "http://schemas.xmlsoap.org/claims/upn", // Name format
                    null, // Name qualifier
                    "user@corewcf.net" // Name
                ),
                attributes
            );

            // Create the SAML assertion
            SamlAssertion samlAssertion = new SamlAssertion(
                assertionId,
                "urn:federation:CoreWCF", // Issuer
                DateTime.UtcNow, // Issue instant
                new SamlConditions(
                    DateTime.UtcNow, // Not before
                    DateTime.UtcNow.AddDays(15), // Not on or after
                    new SamlCondition[] {
                        new SamlAudienceRestrictionCondition(new Uri[] {
                            new Uri("http://service.corewcf.net")
                    }) }),
                null,
                new SamlStatement[] { authStatement, attrStatement }
            );

            var signingCertificate = GenerateSelfSignedCertificate();
            Console.WriteLine("Generated certificate:");
            Console.WriteLine(Convert.ToBase64String(signingCertificate.GetRawCertData()));
            Console.WriteLine();

            if (!X509SubjectKeyIdentifierClause.TryCreateFrom(signingCertificate, out X509SubjectKeyIdentifierClause skiClause))
            {
                throw new InvalidOperationException("Failed to create X509SubjectKeyIdentifierClause from the signing certificate.");
            }

            // Sign the assertion
            samlAssertion.SigningCredentials = new X509SigningCredentials(
                signingCertificate,
                new SecurityKeyIdentifier(skiClause),
                SecurityAlgorithms.RsaSha1Signature,
                SecurityAlgorithms.Sha1Digest);

            var token = new SamlSecurityToken(samlAssertion);

            // Convert to XML
            SamlSecurityTokenHandler tokenHandler = new SamlSecurityTokenHandler();

            using (var stream = new MemoryStream())
            {
                // Configure XML writer settings for pretty printing
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                };

                using (var writer = XmlWriter.Create(stream, settings))
                {
                    tokenHandler.WriteToken(writer, token);
                    writer.Flush();
                    stream.Position = 0;
                    Console.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
                }
            }
        }

        private SamlSubject CreateSamlSubject()
        {
            byte[] randomBytes = new byte[24];

            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(randomBytes);
            }
            var binaryClause = new BinarySecretKeyIdentifierClause(randomBytes);

            SamlSubject subject = new SamlSubject(
                "http://schemas.xmlsoap.org/claims/upn", // Name format
                null, // Name qualifier
                "user@corewcf.net", // Name
                new string[] { "urn:oasis:names:tc:SAML:1.0:cm:holder-of-key" }, // Confirmation methods
                null, // Subject confirmation data
                new SecurityKeyIdentifier(binaryClause) // Key identifier clause
                );

            return subject;
        }

        private SamlAttribute CreateSamlAttribute(string name, string ns, string value)
        {
            return new SamlAttribute(ns, name, new string[] { value });
        }

        private static X509Certificate2 GenerateSelfSignedCertificate()
        {
            // Generate a new RSA key pair
            using (RSACryptoServiceProvider rsa = new RSACryptoServiceProvider(2048))
            {
                // Create certificate name
                string certificateName = "CN=SamlTokenDemo";

                // Create certificate start and end dates
                DateTime notBefore = DateTime.Now;
                DateTime notAfter = notBefore.AddYears(1);

                // Create a certificate request with the RSA key
                var certRequest = new CertificateRequest(
                    certificateName,
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);

                // Add Subject Key Identifier extension
                certRequest.CertificateExtensions.Add(
                    new X509SubjectKeyIdentifierExtension(
                        certRequest.PublicKey,
                        false));

                // Create self-signed certificate
                X509Certificate2 certificate = certRequest.CreateSelfSigned(notBefore, notAfter);

                // Export to PFX (PKCS #12) in memory with a null password
                byte[] pfxData = certificate.Export(X509ContentType.Pfx);

                // Re-import with exportable private key
                return new X509Certificate2(
                    pfxData,
                    (string)null,
                    X509KeyStorageFlags.Exportable | X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
            }
        }
    }
}
#endif
