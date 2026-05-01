// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;
using Xunit;

namespace CoreWCF.Http.Tests.Security
{
    // Regression coverage for COREWCF-2026-006:
    // SamlSerializer.ReadToken used to fall back to a digest-only check
    // (assertion.Signature.SignedInfo.Verify(...)) when the SAML signing token
    // resolved by the out-of-band token resolver was not an X509SecurityToken.
    // That branch never validated <SignatureValue>, so a forged signature would
    // be accepted as long as the (attacker-computable) <DigestValue>s matched.
    public class SamlSerializerSignatureBypassTests
    {
        private const string SamlNs = "urn:oasis:names:tc:SAML:1.0:assertion";
        private const string DsigNs = "http://www.w3.org/2000/09/xmldsig#";
        private const string WsseNs = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        private const string ExcC14N = "http://www.w3.org/2001/10/xml-exc-c14n#";
        private const string EnvelopedSig = "http://www.w3.org/2000/09/xmldsig#enveloped-signature";
        private const string Sha256Digest = "http://www.w3.org/2001/04/xmlenc#sha256";
        private const string HmacSha256 = "http://www.w3.org/2001/04/xmldsig-more#hmac-sha256";
        private const string EncryptedKeySha1ValueType =
            "http://docs.oasis-open.org/wss/oasis-wss-soap-message-security-1.1#EncryptedKeySHA1";
        private const string Base64Binary =
            "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";

        // 256-bit symmetric key used as the (non-X509) signing key for the test.
        private static readonly byte[] s_symmetricKey = new byte[]
        {
            0x4d, 0x39, 0x52, 0x6f, 0x37, 0x65, 0x77, 0x32,
            0x6c, 0x33, 0x67, 0x59, 0x71, 0x77, 0x4f, 0x4e,
            0x6b, 0x4d, 0x35, 0x59, 0x76, 0x7a, 0x74, 0x4d,
            0x6e, 0x66, 0x6c, 0x59, 0x53, 0x4b, 0x69, 0x4f,
        };

        // The KeyIdentifier value the assertion's KeyInfo carries; the resolver
        // ignores it and always returns the same BinarySecretSecurityToken.
        private const string KeyIdentifierValue = "q3vvhP4ipxq3vvhP4ipxq3vvhP4=";

        // Verifies that COREWCF-2026-006 is closed: a SAML assertion whose
        // SignatureValue is garbage (but whose Reference DigestValue is correct)
        // must NOT be accepted, even when the signing token resolves to a
        // non-X509 token.
        [Fact]
        public void ForgedSignatureValue_NonX509SigningToken_IsRejected()
        {
            string assertionId = "_" + Guid.NewGuid().ToString("N");
            string assertionBody = BuildAssertionBody(assertionId);
            string assertionDigest = ComputeReferenceDigestSha256(assertionBody);

            // Garbage SignatureValue: not an HMAC-SHA256 over SignedInfo.
            // Pre-fix code path uses only SignedInfo.Verify() which does not
            // touch this value — so the assertion is wrongly accepted.
            byte[] forgedSignatureBytes = new byte[32];
            for (int i = 0; i < forgedSignatureBytes.Length; i++)
            {
                forgedSignatureBytes[i] = 0xCA;
            }
            string forgedSignatureValueB64 = Convert.ToBase64String(forgedSignatureBytes);

            string forgedAssertion = InsertSignature(
                assertionBody, assertionId, assertionDigest, forgedSignatureValueB64);

            SamlSerializer serializer = new SamlSerializer();
            SecurityTokenResolver resolver = new BinarySecretAlwaysResolver(s_symmetricKey);
            using XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(
                Encoding.UTF8.GetBytes(forgedAssertion), XmlDictionaryReaderQuotas.Max);

            Assert.ThrowsAny<Exception>(() => serializer.ReadToken(
                reader, CoreWCF.Security.WSSecurityTokenSerializer.DefaultInstance, resolver));
        }

        // Sanity check that the fix does not blanket-reject all non-X509 signing
        // tokens: a SAML assertion correctly signed with HMAC-SHA256 using the
        // symmetric key returned by the resolver must still be accepted.
        [Fact]
        public void ValidHmacSignature_NonX509SigningToken_IsAccepted()
        {
            string assertionId = "_" + Guid.NewGuid().ToString("N");
            string assertionBody = BuildAssertionBody(assertionId);
            string assertionDigest = ComputeReferenceDigestSha256(assertionBody);

            string signedInfoXml = BuildSignedInfo(assertionId, assertionDigest);
            byte[] canonicalSignedInfo = CanonicalizeSignedInfoExcC14N(signedInfoXml);
            string signatureValueB64;
            using (HMACSHA256 hmac = new HMACSHA256(s_symmetricKey))
            {
                signatureValueB64 = Convert.ToBase64String(hmac.ComputeHash(canonicalSignedInfo));
            }

            string signedAssertion = InsertSignature(
                assertionBody, assertionId, assertionDigest, signatureValueB64);

            SamlSerializer serializer = new SamlSerializer();
            SecurityTokenResolver resolver = new BinarySecretAlwaysResolver(s_symmetricKey);
            using XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(
                Encoding.UTF8.GetBytes(signedAssertion), XmlDictionaryReaderQuotas.Max);

            SamlSecurityToken token = serializer.ReadToken(
                reader, CoreWCF.Security.WSSecurityTokenSerializer.DefaultInstance, resolver);
            Assert.NotNull(token);
        }

        // Build a minimal SAML 1.1 assertion XML body with no <Signature> child.
        private static string BuildAssertionBody(string assertionId)
        {
            string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            string exp = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ssZ");
            return
                "<saml:Assertion xmlns:saml=\"" + SamlNs + "\" " +
                    "MajorVersion=\"1\" MinorVersion=\"1\" " +
                    "AssertionID=\"" + assertionId + "\" " +
                    "Issuer=\"urn:test:bypass\" IssueInstant=\"" + now + "\">" +
                    "<saml:Conditions NotBefore=\"" + now + "\" NotOnOrAfter=\"" + exp + "\"/>" +
                    "<saml:AttributeStatement>" +
                        "<saml:Subject>" +
                            "<saml:NameIdentifier>victim-admin</saml:NameIdentifier>" +
                            "<saml:SubjectConfirmation>" +
                                "<saml:ConfirmationMethod>urn:oasis:names:tc:SAML:1.0:cm:bearer</saml:ConfirmationMethod>" +
                            "</saml:SubjectConfirmation>" +
                        "</saml:Subject>" +
                        "<saml:Attribute AttributeName=\"role\" AttributeNamespace=\"urn:claims\">" +
                            "<saml:AttributeValue>Administrator</saml:AttributeValue>" +
                        "</saml:Attribute>" +
                    "</saml:AttributeStatement>" +
                "</saml:Assertion>";
        }

        // Compute the SHA-256 digest of the assertion as it would appear after
        // the enveloped-signature transform (no <Signature> present yet) followed
        // by exclusive c14n. This is what gets stored in <Reference><DigestValue>.
        private static string ComputeReferenceDigestSha256(string assertionBody)
        {
            byte[] canonical = CanonicalizeExcC14N(assertionBody);
            using SHA256 sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(canonical));
        }

        // The SignedInfo element by itself is not a stand-alone XML document —
        // its in-scope namespaces depend on the enclosing Signature element.
        // We wrap it into a parent that already declares xmlns:ds, then run
        // exc-c14n only over the SignedInfo NODE (not the whole document) so
        // the result matches what Microsoft.IdentityModel.Xml computes during
        // verification (which canonicalizes the SignedInfo element as it sits
        // inside the parsed Signature element).
        private static byte[] CanonicalizeSignedInfoExcC14N(string signedInfoXml)
        {
            string wrapped = "<ds:Signature xmlns:ds=\"" + DsigNs + "\">" + signedInfoXml + "</ds:Signature>";
            XmlDocument doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(wrapped);
            XmlElement signedInfoElement = (XmlElement)doc.DocumentElement!.FirstChild!;

            XmlNodeList nodeList = signedInfoElement.SelectNodes(".//. | .//@* | .//namespace::*")!;

            XmlDsigExcC14NTransform transform = new XmlDsigExcC14NTransform();
            transform.LoadInput(nodeList);
            using MemoryStream stream = (MemoryStream)transform.GetOutput(typeof(Stream));
            return stream.ToArray();
        }

        // Apply XML-DSIG exclusive c14n to a self-contained XML fragment (used
        // for computing the Reference DigestValue over the assertion body).
        private static byte[] CanonicalizeExcC14N(string xmlFragment)
        {
            XmlDocument doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(xmlFragment);
            XmlDsigExcC14NTransform transform = new XmlDsigExcC14NTransform();
            transform.LoadInput(doc);
            using MemoryStream stream = (MemoryStream)transform.GetOutput(typeof(Stream));
            return stream.ToArray();
        }

        // The SignedInfo element by itself is not a stand-alone XML document —
        // its in-scope namespaces depend on the enclosing Signature element.
        // Wrap it so that exc-c14n produces the same bytes as Microsoft
        // .IdentityModel.Xml does when verifying.
        private static string WrapForCanonicalization(string signedInfoXml)
        {
            return "<ds:Signature xmlns:ds=\"" + DsigNs + "\">" + signedInfoXml + "</ds:Signature>";
        }

        private static string BuildSignedInfo(string assertionId, string digestB64)
        {
            return
                "<ds:SignedInfo xmlns:ds=\"" + DsigNs + "\">" +
                    "<ds:CanonicalizationMethod Algorithm=\"" + ExcC14N + "\"/>" +
                    "<ds:SignatureMethod Algorithm=\"" + HmacSha256 + "\"/>" +
                    "<ds:Reference URI=\"#" + assertionId + "\">" +
                        "<ds:Transforms>" +
                            "<ds:Transform Algorithm=\"" + EnvelopedSig + "\"/>" +
                            "<ds:Transform Algorithm=\"" + ExcC14N + "\"/>" +
                        "</ds:Transforms>" +
                        "<ds:DigestMethod Algorithm=\"" + Sha256Digest + "\"/>" +
                        "<ds:DigestValue>" + digestB64 + "</ds:DigestValue>" +
                    "</ds:Reference>" +
                "</ds:SignedInfo>";
        }

        // Insert a ds:Signature as the last child of the <saml:Assertion> using
        // the given DigestValue and SignatureValue. The KeyInfo carries a
        // wsse:SecurityTokenReference / wsse:KeyIdentifier (no X509Data), which
        // forces the SamlSerializer down the non-X509 else-branch when the
        // resolver returns a BinarySecretSecurityToken.
        private static string InsertSignature(
            string assertionBody,
            string assertionId,
            string digestB64,
            string signatureValueB64)
        {
            string signedInfo = BuildSignedInfo(assertionId, digestB64);
            string signatureXml =
                "<ds:Signature xmlns:ds=\"" + DsigNs + "\">" +
                    signedInfo +
                    "<ds:SignatureValue>" + signatureValueB64 + "</ds:SignatureValue>" +
                    "<ds:KeyInfo>" +
                        "<o:SecurityTokenReference xmlns:o=\"" + WsseNs + "\">" +
                            "<o:KeyIdentifier ValueType=\"" + EncryptedKeySha1ValueType + "\" " +
                                "EncodingType=\"" + Base64Binary + "\">" +
                                KeyIdentifierValue +
                            "</o:KeyIdentifier>" +
                        "</o:SecurityTokenReference>" +
                    "</ds:KeyInfo>" +
                "</ds:Signature>";

            const string closingTag = "</saml:Assertion>";
            int idx = assertionBody.LastIndexOf(closingTag, StringComparison.Ordinal);
            return assertionBody.Substring(0, idx) + signatureXml + closingTag;
        }

        // A token resolver that always resolves to the same BinarySecretSecurityToken
        // (a non-X509 SecurityToken) regardless of which key identifier clause
        // the SAML KeyInfo carries. This simulates a service whose
        // out-of-band issuer-token resolver is configured with a symmetric proof
        // key (for example from a WS-Trust symmetric-key holder-of-key STS) —
        // exactly the precondition described in COREWCF-2026-006.
        private sealed class BinarySecretAlwaysResolver : SecurityTokenResolver
        {
            private readonly BinarySecretSecurityToken _token;
            private readonly SecurityKey _key;

            public BinarySecretAlwaysResolver(byte[] symmetricKeyBytes)
            {
                _token = new BinarySecretSecurityToken(symmetricKeyBytes);
                _key = _token.SecurityKeys[0];
            }

            protected override bool TryResolveTokenCore(SecurityKeyIdentifier keyIdentifier, out SecurityToken token)
            {
                token = _token;
                return true;
            }

            protected override bool TryResolveTokenCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityToken token)
            {
                token = _token;
                return true;
            }

            protected override bool TryResolveSecurityKeyCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityKey key)
            {
                key = _key;
                return true;
            }
        }
    }
}
