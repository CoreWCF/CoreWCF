// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using Xunit;

namespace CoreWCF.Http.Tests.Security
{
    public class SamlSerializerTests
    {
        private static readonly string s_samlXml = @"<?xml version=""1.0"" encoding=""utf-8""?><saml:Assertion MajorVersion=""1"" MinorVersion=""1"" AssertionID=""uuid-bef407e4-7d64-4800-8659-adea14951934"" Issuer=""urn:federation:CoreWCF"" IssueInstant=""2025-07-10T21:04:18.139Z"" xmlns:saml=""urn:oasis:names:tc:SAML:1.0:assertion""><saml:Conditions NotBefore=""2025-07-10T21:04:18.139Z"" NotOnOrAfter=""2025-07-25T21:04:18.139Z""><saml:AudienceRestrictionCondition><saml:Audience>http://service.corewcf.net</saml:Audience></saml:AudienceRestrictionCondition></saml:Conditions><saml:AuthenticationStatement AuthenticationMethod=""urn:oasis:names:tc:SAML:1.0:am:password"" AuthenticationInstant=""2025-07-10T21:04:18.139Z""><saml:Subject><saml:NameIdentifier Format=""http://schemas.xmlsoap.org/claims/upn"">user@corewcf.net</saml:NameIdentifier><saml:SubjectConfirmation><saml:ConfirmationMethod>urn:oasis:names:tc:SAML:1.0:cm:holder-of-key</saml:ConfirmationMethod><KeyInfo xmlns=""http://www.w3.org/2000/09/xmldsig#""><trust:BinarySecret xmlns:trust=""http://docs.oasis-open.org/ws-sx/ws-trust/200512"">PXYnnT/NxMlCJ4SbUHjkFh45Zv5TrR0N</trust:BinarySecret></KeyInfo></saml:SubjectConfirmation></saml:Subject></saml:AuthenticationStatement><saml:AttributeStatement><saml:Subject><saml:NameIdentifier Format=""http://schemas.xmlsoap.org/claims/upn"">user@corewcf.net</saml:NameIdentifier></saml:Subject><saml:Attribute AttributeName=""EmailAddress"" AttributeNamespace=""http://schemas.xmlsoap.org/claims""><saml:AttributeValue>user@corewcf.net</saml:AttributeValue></saml:Attribute><saml:Attribute AttributeName=""RequestorDomain"" AttributeNamespace=""http://schemas.microsoft.com/ws/2006/04/identity/claims""><saml:AttributeValue>example.com</saml:AttributeValue></saml:Attribute><saml:Attribute AttributeName=""action"" AttributeNamespace=""http://schemas.xmlsoap.org/ws/2006/12/authorization/claims""><saml:AttributeValue>FooService.ShowGroups</saml:AttributeValue></saml:Attribute><saml:Attribute AttributeName=""ThirdPartyRequested"" AttributeNamespace=""http://schemas.microsoft.com/ws/2006/04/identity/claims""><saml:AttributeValue>True</saml:AttributeValue></saml:Attribute></saml:AttributeStatement><Signature xmlns=""http://www.w3.org/2000/09/xmldsig#""><SignedInfo><CanonicalizationMethod Algorithm=""http://www.w3.org/2001/10/xml-exc-c14n#"" /><SignatureMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#rsa-sha1"" /><Reference URI=""#uuid-bef407e4-7d64-4800-8659-adea14951934""><Transforms><Transform Algorithm=""http://www.w3.org/2000/09/xmldsig#enveloped-signature"" /><Transform Algorithm=""http://www.w3.org/2001/10/xml-exc-c14n#"" /></Transforms><DigestMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#sha1"" /><DigestValue>b9umkbPu51reXhC2Q61VglCXq1Y=</DigestValue></Reference></SignedInfo><SignatureValue>kHmzMZJWZ9BQyKw6ImUbtWhmL0+FqAbohB5c7aFcUwXja+iuFVZ+vv6faieyuAQWJ58pdKSSwjFT00c2I0refe1zAb6BreUJ/cDYq0x5a4yOc4FOjBBKRaqJBlRoMO32GiX2/V4UEotv7j/gk7V01Gp8Ygf92F/+rKUswUztRjACeiEEjhTfoUlj8VQKswQOEvOuqd/4WdYFxQVV5rN/vBnXKnTsUSwrCd3mkh75j7bpAq+08Jn0XAIpTZxzr4cgXTy7L3J4KR/jxmXkZe02D8siJkGujfim38t5xBFkx8DOssudmQBUyjcypz3FF03j57eg7At2DgSXpdU8oNI4tw==</SignatureValue><KeyInfo><o:SecurityTokenReference xmlns:o=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd""><o:KeyIdentifier ValueType=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509SubjectKeyIdentifier"" EncodingType=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary"">n/c1aiQCuxAWneCsTdfxYXgt0Qs=</o:KeyIdentifier></o:SecurityTokenReference></KeyInfo></Signature></saml:Assertion>";
        private static readonly string s_signingCertificate = @"MIIC1DCCAbygAwIBAgIJAJHux5HhbLFgMA0GCSqGSIb3DQEBCwUAMBgxFjAUBgNVBAMTDVNhbWxUb2tlbkRlbW8wHhcNMjUwNzEwMjEwNDE4WhcNMjYwNzEwMjEwNDE4WjAYMRYwFAYDVQQDEw1TYW1sVG9rZW5EZW1vMIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwsTJi52PaLjoM2wgkkKOh4f0AmjuoRoNKIGaz/bCPZoO1biVePgDOOF/Ezcw70mB66xqACF6GGioUS87KfjqgWaFMV8Mfc/tS38D0r5GQXUQnc8YFQ8R3K1K05sK6HlNNECIlUSGrnfMV5SLgELLVxRMBZHJ4rmPMnpkf90YrhZM7OCw5c+FrwDY3wGtB8xaHtuzCdnAHuR2QRum8EPpfUKlwXn3Egc2tRN8ViqLwtnxiD/BUJhtLCrNOeAU8SrekOdyybf3iC7DzZFYpKUQib/wwz8p5FOShHCpOGcfuZq44nSsEyR/Se+wWM7w5eUwaWjgiwR6HXkWL93dl3jTHQIDAQABoyEwHzAdBgNVHQ4EFgQUn/c1aiQCuxAWneCsTdfxYXgt0QswDQYJKoZIhvcNAQELBQADggEBAJJWkzyBUnVLuPRyquTdlq77vOl6o4DAfVadSJCs60sucekplJy/oBLbsDk9zsgFrybEbMFiISDAw6FyK4LGaERhChf3t/SrzsJevemPxw78R+vqdOmb6KGbuVTPqZmTIzYmkPYLbxmYfPwUu3SN6tcgF2KiYpIXynFTK1eol7BJRKXakIQ4ZTqdvk7IwTXOFwK3qclpmn+pUk8OZjoAAjJKpRt+wk+IQ4Q2+t5KWDaXdNDCcS5scV02ahhmBf+oLXZUgiZw9S646n/sSOjF1Ny93GiAusmgZ6hj2nBHP4YlQE0o8Lq0KAhgnjqHYCqDPPFsC7+k4BB4q2GlAghT0YM=";
        private static readonly string s_proofTokenSymmetricKeyString = "PXYnnT/NxMlCJ4SbUHjkFh45Zv5TrR0N";
        private static readonly byte[] s_proofTokenSymmetricKey = Convert.FromBase64String(s_proofTokenSymmetricKeyString);

        [Fact]
        public void TestSamlDeserialization()
        {
            var samlSerializer = new SamlSerializer();
            List<SecurityToken> securityTokenList = new List<SecurityToken>();
            X509Certificate2 x509Cert = new(Convert.FromBase64String(s_signingCertificate));
            securityTokenList.Add(new X509SecurityToken(x509Cert));
            SecurityTokenResolver securityTokenResolver = SecurityTokenResolver.CreateDefaultSecurityTokenResolver(securityTokenList.AsReadOnly(), true);
            var reader = XmlDictionaryReader.CreateTextReader(Encoding.UTF8.GetBytes(s_samlXml), XmlDictionaryReaderQuotas.Max);
            SamlSecurityToken samlToken = samlSerializer.ReadToken(reader, CoreWCF.Security.WSSecurityTokenSerializer.DefaultInstance, securityTokenResolver);
            var imSamlToken = (Microsoft.IdentityModel.Tokens.Saml.SamlSecurityToken)samlToken;
            Assert.NotNull(imSamlToken);
            var samlAssertion = imSamlToken.Assertion;

            // <saml:Assertion> attributes
            Assert.Equal("uuid-bef407e4-7d64-4800-8659-adea14951934", samlAssertion.AssertionId);
            Assert.Equal("urn:federation:CoreWCF", samlAssertion.Issuer);
            Assert.Equal(DateTime.Parse("2025-07-10T21:04:18.139Z", null, DateTimeStyles.AdjustToUniversal), samlAssertion.IssueInstant);

            // <saml:Conditions> attributes
            Assert.Equal(DateTime.Parse("2025-07-10T21:04:18.139Z", null, DateTimeStyles.AdjustToUniversal), samlAssertion.Conditions.NotBefore);
            Assert.Equal(DateTime.Parse("2025-07-25T21:04:18.139Z", null, DateTimeStyles.AdjustToUniversal), samlAssertion.Conditions.NotOnOrAfter);
            Assert.Single(samlAssertion.Conditions.Conditions);

            // <saml:AudienceRestrictionCondition>
            Assert.IsType<Microsoft.IdentityModel.Tokens.Saml.SamlAudienceRestrictionCondition>(samlAssertion.Conditions.Conditions.First());
            var audience = samlAssertion.Conditions.Conditions.First() as Microsoft.IdentityModel.Tokens.Saml.SamlAudienceRestrictionCondition;
            Assert.Single(audience.Audiences);
            Assert.Equal(new Uri("http://service.corewcf.net"), audience.Audiences.First());

            // Check there are two statements in the assertion
            Assert.Equal(2, samlAssertion.Statements.Count);

            // <saml:AuthenticationStatement>
            Assert.IsType<Microsoft.IdentityModel.Tokens.Saml.SamlAuthenticationStatement>(samlAssertion.Statements.First());
            var authStatement = samlAssertion.Statements.First() as Microsoft.IdentityModel.Tokens.Saml.SamlAuthenticationStatement;
            Assert.Equal("urn:oasis:names:tc:SAML:1.0:am:password", authStatement.AuthenticationMethod);
            Assert.Equal(DateTime.Parse("2025-07-10T21:04:18.139Z", null, DateTimeStyles.AdjustToUniversal), authStatement.AuthenticationInstant);
            Assert.NotNull(authStatement.Subject);
            Assert.Equal("user@corewcf.net", authStatement.Subject.Name);
            Assert.Equal("http://schemas.xmlsoap.org/claims/upn", authStatement.Subject.NameFormat);
            Assert.Single(authStatement.Subject.ConfirmationMethods);
            Assert.Equal("urn:oasis:names:tc:SAML:1.0:cm:holder-of-key", authStatement.Subject.ConfirmationMethods.First());
            Assert.IsType<IdentityModel.Tokens.Saml.KeyInfo>(authStatement.Subject.KeyInfo);
            var keyInfo = authStatement.Subject.KeyInfo as IdentityModel.Tokens.Saml.KeyInfo;
            Assert.NotNull(keyInfo);
            Assert.Null(keyInfo.SecurityTokenReference);
            Assert.Null(keyInfo.EncryptedKey);
            Assert.NotNull(keyInfo.BinarySecret);
            Assert.Equal(s_proofTokenSymmetricKeyString, keyInfo.BinarySecret.Value);
            Assert.Equal(s_proofTokenSymmetricKey, keyInfo.BinarySecret.GetBytes());

            // <saml:AttributeStatement>
            Assert.IsType<Microsoft.IdentityModel.Tokens.Saml.SamlAttributeStatement>(samlAssertion.Statements.Last());
            var attributeStatement = samlAssertion.Statements.Last() as Microsoft.IdentityModel.Tokens.Saml.SamlAttributeStatement;
            Assert.NotNull(attributeStatement.Subject);
            Assert.Equal("http://schemas.xmlsoap.org/claims/upn", attributeStatement.Subject.NameFormat);
            Assert.Equal("user@corewcf.net", attributeStatement.Subject.Name);
            Assert.Empty(attributeStatement.Subject.ConfirmationMethods);
            Assert.Equal(4, attributeStatement.Attributes.Count);
            var attributes = attributeStatement.Attributes.ToArray();
            var emailAttribute = attributes[0];
            Assert.Equal("EmailAddress", emailAttribute.Name);
            Assert.Equal("http://schemas.xmlsoap.org/claims", emailAttribute.Namespace);
            Assert.Single(emailAttribute.Values);
            Assert.Equal("user@corewcf.net", emailAttribute.Values.First());
            var requestorDomainAttribute = attributes[1];
            Assert.Equal("RequestorDomain", requestorDomainAttribute.Name);
            Assert.Equal("http://schemas.microsoft.com/ws/2006/04/identity/claims", requestorDomainAttribute.Namespace);
            Assert.Single(requestorDomainAttribute.Values);
            Assert.Equal("example.com", requestorDomainAttribute.Values.First());
            var actionAttribute = attributes[2];
            Assert.Equal("action", actionAttribute.Name);
            Assert.Equal("http://schemas.xmlsoap.org/ws/2006/12/authorization/claims", actionAttribute.Namespace);
            Assert.Single(actionAttribute.Values);
            Assert.Equal("FooService.ShowGroups", actionAttribute.Values.First());
            var thirdPartyRequestedAttribute = attributes[3];
            Assert.Equal("ThirdPartyRequested", thirdPartyRequestedAttribute.Name);
            Assert.Equal("http://schemas.microsoft.com/ws/2006/04/identity/claims", thirdPartyRequestedAttribute.Namespace);
            Assert.Single(thirdPartyRequestedAttribute.Values);
            Assert.Equal("True", thirdPartyRequestedAttribute.Values.First());

            // <Signature>
            Assert.NotNull(samlAssertion.Signature);
            var signature = samlAssertion.Signature;
            Assert.NotNull(signature.SignedInfo);
            var signedInfo = signature.SignedInfo;
            Assert.Equal("http://www.w3.org/2001/10/xml-exc-c14n#", signedInfo.CanonicalizationMethod);
            Assert.Equal("http://www.w3.org/2000/09/xmldsig#rsa-sha1", signedInfo.SignatureMethod);
            Assert.Single(signedInfo.References);
            var reference = signedInfo.References.First();
            Assert.Equal("#uuid-bef407e4-7d64-4800-8659-adea14951934", reference.Uri);
            Assert.Equal("http://www.w3.org/2000/09/xmldsig#sha1", reference.DigestMethod);
            Assert.Equal("b9umkbPu51reXhC2Q61VglCXq1Y=", reference.DigestValue);

            // <KeyInfo>
            Assert.NotNull(signature.KeyInfo);
            Assert.IsType<IdentityModel.Tokens.Saml.KeyInfo>(signature.KeyInfo);
            var keyInfoElement = signature.KeyInfo as IdentityModel.Tokens.Saml.KeyInfo;
            Assert.Null(keyInfoElement.BinarySecret);
            Assert.Null(keyInfoElement.EncryptedKey);
            Assert.NotNull(keyInfoElement.SecurityTokenReference);
            Assert.NotNull(keyInfoElement.SecurityTokenReference.SecurityKeyIdentifier);
            Assert.Equal("http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509SubjectKeyIdentifier", keyInfoElement.SecurityTokenReference.SecurityKeyIdentifier.ValueType);
            Assert.Equal("http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary", keyInfoElement.SecurityTokenReference.SecurityKeyIdentifier.EncodingType);
            var skiExtension = x509Cert.Extensions[new X509SubjectKeyIdentifierExtension().Oid.Value] as X509SubjectKeyIdentifierExtension;

            byte[] skiBytes = new byte[skiExtension.SubjectKeyIdentifier.Length / 2];
            for (int i = 0; i < skiExtension.SubjectKeyIdentifier.Length; i += 2)
            {
                skiBytes[i / 2] = Convert.ToByte(skiExtension.SubjectKeyIdentifier.Substring(i, 2), 16);
            }

            Assert.Equal(Convert.ToBase64String(skiBytes), keyInfoElement.SecurityTokenReference.SecurityKeyIdentifier.Value);
        }


    }
}
