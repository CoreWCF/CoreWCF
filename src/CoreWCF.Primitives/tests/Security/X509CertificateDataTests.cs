// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;
using Xunit;
using MsTokens = Microsoft.IdentityModel.Tokens;
using MsXml = Microsoft.IdentityModel.Xml;

namespace CoreWCF.Primitives.Tests.Security
{
    [CollectionDefinition("X509 certificate data", DisableParallelization = true)]
    public class X509CertificateDataCollection
    {
    }

    [Collection("X509 certificate data")]
    public class X509CertificateDataTests : IDisposable
    {
        private const string AllowNonCertificateDataSwitch = "CoreWCF.Security.AllowNonCertificateX509Data";
        private const string XmlSignatureNamespace = "http://www.w3.org/2000/09/xmldsig#";
        private const string SecurityNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        private const string UtilityNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
        private const string X509ValueType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3";
        private const string EncodingTypePrefix = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#";
        private const string ValidationPropertyPrefix = "CoreWCF.IdentityModel.Tokens.SamlTokenValidationParameters.";
        private readonly bool _previousAllowNonCertificateData;

        private delegate bool TryCreateCertificate(byte[] rawData, out X509Certificate2 certificate);

        public X509CertificateDataTests()
        {
            _previousAllowNonCertificateData = AppContext.TryGetSwitch(AllowNonCertificateDataSwitch, out bool enabled) && enabled;
            AppContext.SetSwitch(AllowNonCertificateDataSwitch, false);
        }

        public void Dispose()
        {
            AppContext.SetSwitch(AllowNonCertificateDataSwitch, _previousAllowNonCertificateData);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CertificateDataIsAccepted(bool allowNonCertificateData)
        {
            AppContext.SetSwitch(AllowNonCertificateDataSwitch, allowNonCertificateData);
            using X509Certificate2 certificate = CreateCertificate();
            AssertAccepted(certificate.RawData, certificate);

            X509RawDataKeyIdentifierClause clause = new X509RawDataKeyIdentifierClause(certificate);
            Assert.True(clause.Matches(certificate));
            Assert.NotNull(clause.CreateKey());
        }

        [Theory]
        [InlineData(X509ContentType.Pkcs12)]
        [InlineData(X509ContentType.Pkcs7)]
        public void NonCertificateDataIsRejected(X509ContentType contentType)
        {
            using X509Certificate2 certificate = CreateCertificate();
            byte[] data = Export(certificate, contentType);
            Assert.Equal(contentType, X509Certificate2.GetCertContentType(data));
            AssertRejected(data);
        }

        [WindowsOnlyTheory]
        [InlineData(X509ContentType.SerializedCert)]
        [InlineData(X509ContentType.SerializedStore)]
        public void WindowsSerializedDataIsRejected(X509ContentType contentType)
        {
            using X509Certificate2 certificate = CreateCertificate();
            byte[] data = Export(certificate, contentType);
            Assert.Equal(contentType, X509Certificate2.GetCertContentType(data));
            AssertRejected(data);
        }

        [WindowsOnlyTheory]
        [InlineData(X509ContentType.Pkcs12)]
        [InlineData(X509ContentType.Pkcs7)]
        [InlineData(X509ContentType.SerializedCert)]
        public void AppContextSwitchRestoresLegacyFormats(X509ContentType contentType)
        {
            AppContext.SetSwitch(AllowNonCertificateDataSwitch, true);
            using X509Certificate2 certificate = CreateCertificate();
            AssertAccepted(Export(certificate, contentType), certificate);
        }

        [Fact]
        public void InvalidCertificateDataIsRejected()
        {
            AssertRejected(new byte[] { 1, 2, 3 });
        }

        [Fact]
        public void EmptyCertificateDataStillCannotCreateCertificate()
        {
            TryCreateCertificate tryCreate = GetMethod<TryCreateCertificate>(
                "CoreWCF.Security.SecurityUtils", "TryCreateX509CertificateFromRawData");
            Assert.False(tryCreate(null, out X509Certificate2 nullCertificate));
            Assert.Null(nullCertificate);
            Assert.False(tryCreate(Array.Empty<byte>(), out X509Certificate2 emptyCertificate));
            Assert.Null(emptyCertificate);
        }

        [WindowsOnlyFact]
        public void PreviouslyAcceptedRawClausesAreCheckedAgainBeforeImport()
        {
            using X509Certificate2 certificate = CreateCertificate();
            AppContext.SetSwitch(AllowNonCertificateDataSwitch, true);
            X509RawDataKeyIdentifierClause clause = new X509RawDataKeyIdentifierClause(
                Export(certificate, X509ContentType.SerializedCert));
            AppContext.SetSwitch(AllowNonCertificateDataSwitch, false);

            Assert.Throws<CryptographicException>(() => clause.CreateKey());
            IssuerTokenResolver resolver = new IssuerTokenResolver();
            Assert.Throws<CryptographicException>(() => resolver.ResolveToken(clause));

            Func<SecurityKeyIdentifier, SecurityTokenResolver, SecurityToken> resolveSamlToken =
                GetMethod<Func<SecurityKeyIdentifier, SecurityTokenResolver, SecurityToken>>(
                    "CoreWCF.IdentityModel.Tokens.SamlSerializer", "ResolveSecurityToken");
            Assert.Throws<CryptographicException>(() =>
                resolveSamlToken(new SecurityKeyIdentifier(clause), null));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void SamlSigningCertificatesRequireCertificateData(bool saml2)
        {
            using X509Certificate2 certificate = CreateCertificate();
            AssertSamlAccepted(certificate.RawData, saml2);
            AssertSamlRejected(Export(certificate, X509ContentType.Pkcs12), saml2);
        }

        [WindowsOnlyTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void SamlSigningCertificatesRejectSerializedDataUnlessOptedOut(bool saml2)
        {
            using X509Certificate2 certificate = CreateCertificate();
            byte[] data = Export(certificate, X509ContentType.SerializedCert);
            AssertSamlRejected(data, saml2);
            AppContext.SetSwitch(AllowNonCertificateDataSwitch, true);
            AssertSamlAccepted(data, saml2);
        }

        private static void AssertRejected(byte[] data)
        {
            Func<byte[], X509Certificate2> create = GetMethod<Func<byte[], X509Certificate2>>(
                "CoreWCF.Security.SecurityUtils", "CreateCertificateFromRawData");
            Assert.Throws<CryptographicException>(() => create(data));
            Assert.Throws<CryptographicException>(() => new X509RawDataKeyIdentifierClause(data));
            Assert.Throws<CryptographicException>(() => ReadToken(data, false, false));
            Assert.Throws<CryptographicException>(() => ReadToken(data, true, false));
            Assert.Throws<CryptographicException>(() => ReadToken(data, false, true));
            Assert.Throws<CryptographicException>(() => ReadToken(data, true, true));
            Assert.Throws<CryptographicException>(() => ReadClause(data, false));
            Assert.Throws<MessageSecurityException>(() => ReadClause(data, true));
            Assert.Throws<CryptographicException>(() => ReadEndpointCertificate(data));
            Assert.Throws<CryptographicException>(() => ReadClaimSet(data));
        }

        private static void AssertAccepted(byte[] data, X509Certificate2 certificate)
        {
            Func<byte[], X509Certificate2> create = GetMethod<Func<byte[], X509Certificate2>>(
                "CoreWCF.Security.SecurityUtils", "CreateCertificateFromRawData");
            using X509Certificate2 imported = create(data);
            Assert.Equal(certificate.RawData, imported.RawData);

            X509RawDataKeyIdentifierClause clause = new X509RawDataKeyIdentifierClause(data);
            Assert.Equal(data, clause.GetX509RawData());
            Assert.NotNull(clause.CreateKey());
            Assert.Equal(data, ReadClause(data, false).GetX509RawData());
            Assert.Equal(certificate.RawData, ReadClause(data, true).GetX509RawData());

            foreach (bool protocolSerializer in new[] { false, true })
            {
                foreach (bool hex in new[] { false, true })
                {
                    using X509SecurityToken token = ReadToken(data, hex, protocolSerializer);
                    Assert.Equal("certificate-token", token.Id);
                    Assert.Equal(certificate.RawData, token.Certificate.RawData);
                }
            }

            using X509Certificate2 endpointCertificate = ReadEndpointCertificate(data);
            Assert.Equal(certificate.RawData, endpointCertificate.RawData);
            using X509CertificateClaimSet claimSet = ReadClaimSet(data);
            Assert.Equal(certificate.RawData, claimSet.X509Certificate.RawData);
        }

        private static X509SecurityToken ReadToken(byte[] data, bool hex, bool protocolSerializer)
        {
            string encoding = hex ? "HexBinary" : "Base64Binary";
            string encodedData = hex ? BitConverter.ToString(data).Replace("-", string.Empty) : Convert.ToBase64String(data);
            string xml = $"<wsse:BinarySecurityToken xmlns:wsse=\"{SecurityNamespace}\" xmlns:wsu=\"{UtilityNamespace}\" wsu:Id=\"certificate-token\" ValueType=\"{X509ValueType}\" EncodingType=\"{EncodingTypePrefix}{encoding}\">{encodedData}</wsse:BinarySecurityToken>";
            using StringReader textReader = new StringReader(xml);
            using XmlReader reader = XmlReader.Create(textReader);
            reader.MoveToContent();
            SecurityToken token = protocolSerializer
                ? new WSSecurityTokenSerializer(SecurityVersion.WSSecurity10).ReadToken(reader, null)
                : new X509SecurityTokenHandler().ReadToken(reader);
            return Assert.IsType<X509SecurityToken>(token);
        }

        private static X509RawDataKeyIdentifierClause ReadClause(byte[] data, bool protocolSerializer)
        {
            using StringReader textReader = new StringReader(X509DataXml(data));
            using XmlReader reader = XmlReader.Create(textReader);
            reader.MoveToContent();
            SecurityKeyIdentifierClause clause = protocolSerializer
                ? new WSSecurityTokenSerializer(SecurityVersion.WSSecurity10).ReadKeyIdentifierClause(reader)
                : new X509DataSecurityKeyIdentifierClauseSerializer().ReadKeyIdentifierClause(reader);
            return Assert.IsType<X509RawDataKeyIdentifierClause>(clause);
        }

        private static X509Certificate2 ReadEndpointCertificate(byte[] data)
        {
            using StringReader textReader = new StringReader(X509DataXml(data));
            using XmlReader xmlReader = XmlReader.Create(textReader);
            using XmlDictionaryReader reader = XmlDictionaryReader.CreateDictionaryReader(xmlReader);
            ConstructorInfo constructor = typeof(X509CertificateEndpointIdentity).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(XmlDictionaryReader) }, null);
            X509CertificateEndpointIdentity identity = (X509CertificateEndpointIdentity)Invoke(
                () => constructor.Invoke(new object[] { reader }));
            return Assert.Single(identity.Certificates.Cast<X509Certificate2>());
        }

        private static X509CertificateClaimSet ReadClaimSet(byte[] data)
        {
            string xml = $"<X509CertificateClaimSet>{Convert.ToBase64String(data)}</X509CertificateClaimSet>";
            using StringReader textReader = new StringReader(xml);
            using XmlReader xmlReader = XmlReader.Create(textReader);
            using XmlDictionaryReader reader = XmlDictionaryReader.CreateDictionaryReader(xmlReader);
            reader.MoveToContent();
            Assembly assembly = typeof(X509SecurityToken).Assembly;
            Type dictionaryType = assembly.GetType("CoreWCF.Security.SctClaimDictionary", throwOnError: true);
            object dictionary = dictionaryType.GetProperty("Instance").GetValue(null);
            Type serializerType = assembly.GetType("CoreWCF.Security.SctClaimSerializer", throwOnError: true);
            MethodInfo deserialize = serializerType.GetMethod("DeserializeClaimSet", BindingFlags.Public | BindingFlags.Static);
            return (X509CertificateClaimSet)Invoke(
                () => deserialize.Invoke(null, new object[] { reader, dictionary, null, null }));
        }

        private static void AssertSamlAccepted(byte[] data, bool saml2)
        {
            MsTokens.SecurityToken token = CreateSamlToken(data, saml2);
            MsTokens.TokenValidationParameters parameters = CreateValidationParameters();
            object adapter = CreateSamlValidationAdapter();
            Func<string, MsTokens.SecurityToken, string, MsTokens.TokenValidationParameters, IEnumerable<MsTokens.SecurityKey>> resolve =
                GetMethod<Func<string, MsTokens.SecurityToken, string, MsTokens.TokenValidationParameters, IEnumerable<MsTokens.SecurityKey>>>(
                    adapter.GetType().FullName, "ResolveIssuerSigningKey", adapter);
            MsTokens.X509SecurityKey key = Assert.IsType<MsTokens.X509SecurityKey>(Assert.Single(resolve(null, token, null, parameters)));
            using X509Certificate2 certificate = key.Certificate;
            Func<MsTokens.SecurityKey, MsTokens.SecurityToken, MsTokens.TokenValidationParameters, bool> validate =
                GetMethod<Func<MsTokens.SecurityKey, MsTokens.SecurityToken, MsTokens.TokenValidationParameters, bool>>(
                    adapter.GetType().FullName, "IssuerSigningKeyValidator", adapter);
            Assert.True(validate(key, token, parameters));
        }

        private static void AssertSamlRejected(byte[] data, bool saml2)
        {
            MsTokens.SecurityToken token = CreateSamlToken(data, saml2);
            MsTokens.TokenValidationParameters parameters = CreateValidationParameters();
            object adapter = CreateSamlValidationAdapter();
            Func<string, MsTokens.SecurityToken, string, MsTokens.TokenValidationParameters, IEnumerable<MsTokens.SecurityKey>> resolve =
                GetMethod<Func<string, MsTokens.SecurityToken, string, MsTokens.TokenValidationParameters, IEnumerable<MsTokens.SecurityKey>>>(
                    adapter.GetType().FullName, "ResolveIssuerSigningKey", adapter);
            Assert.Throws<CryptographicException>(() => resolve(null, token, null, parameters).ToList());
            Func<MsTokens.SecurityKey, MsTokens.SecurityToken, MsTokens.TokenValidationParameters, bool> validate =
                GetMethod<Func<MsTokens.SecurityKey, MsTokens.SecurityToken, MsTokens.TokenValidationParameters, bool>>(
                    adapter.GetType().FullName, "IssuerSigningKeyValidator", adapter);
            Assert.Throws<CryptographicException>(() => validate(null, token, parameters));
        }

        private static object CreateSamlValidationAdapter()
        {
            Type type = typeof(X509SecurityToken).Assembly.GetType(
                "CoreWCF.IdentityModel.Tokens.SamlTokenValidationParameters", throwOnError: true);
            return Activator.CreateInstance(type);
        }

        private static MsTokens.TokenValidationParameters CreateValidationParameters()
        {
            return new MsTokens.TokenValidationParameters
            {
                PropertyBag = new Dictionary<string, object>
                {
                    [ValidationPropertyPrefix + "SamlSecurityToken"] = null,
                    [ValidationPropertyPrefix + "SecurityTokenHandlerConfiguration"] = new SecurityTokenHandlerConfiguration
                    {
                        CertificateValidator = X509CertificateValidator.None
                    }
                }
            };
        }

        private static MsTokens.SecurityToken CreateSamlToken(byte[] data, bool saml2)
        {
            MsXml.X509Data x509Data = new MsXml.X509Data();
            x509Data.Certificates.Add(Convert.ToBase64String(data));
            MsXml.KeyInfo keyInfo = new MsXml.KeyInfo();
            keyInfo.X509Data.Add(x509Data);
            MsXml.Signature signature = new MsXml.Signature { KeyInfo = keyInfo };

            if (saml2)
            {
                MsTokens.Saml2.Saml2Assertion assertion = new MsTokens.Saml2.Saml2Assertion(
                    new MsTokens.Saml2.Saml2NameIdentifier("urn:test:issuer"))
                {
                    Signature = signature
                };
                return new MsTokens.Saml2.Saml2SecurityToken(assertion);
            }

            MsTokens.Saml.SamlAttributeStatement statement = new MsTokens.Saml.SamlAttributeStatement(
                new MsTokens.Saml.SamlSubject(null, null, "test"),
                new[] { new MsTokens.Saml.SamlAttribute("name", "urn:test", new[] { "value" }) });
            MsTokens.Saml.SamlAssertion samlAssertion = new MsTokens.Saml.SamlAssertion(
                "_certificate", "urn:test:issuer", DateTime.UtcNow, null, null, new[] { statement })
            {
                Signature = signature
            };
            return new MsTokens.Saml.SamlSecurityToken(samlAssertion);
        }

        private static T GetMethod<T>(string typeName, string methodName, object target = null) where T : Delegate
        {
            Type type = typeof(X509SecurityToken).Assembly.GetType(typeName, throwOnError: true);
            MethodInfo method = type.GetMethod(methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
            return (T)(target == null ? method.CreateDelegate(typeof(T)) : method.CreateDelegate(typeof(T), target));
        }

        private static object Invoke(Func<object> action)
        {
            try
            {
                return action();
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static string X509DataXml(byte[] data)
        {
            return $"<ds:X509Data xmlns:ds=\"{XmlSignatureNamespace}\"><ds:X509Certificate>{Convert.ToBase64String(data)}</ds:X509Certificate></ds:X509Data>";
        }

        private static byte[] Export(X509Certificate2 certificate, X509ContentType contentType)
        {
            if (contentType == X509ContentType.Pkcs7)
            {
                SignedCms message = new SignedCms(new ContentInfo(new byte[] { 1, 2, 3 }));
                CmsSigner signer = new CmsSigner(certificate)
                {
                    IncludeOption = X509IncludeOption.EndCertOnly
                };
                message.ComputeSignature(signer);
                return message.Encode();
            }

            return contentType == X509ContentType.SerializedStore
                ? new X509Certificate2Collection(certificate).Export(contentType)
                : certificate.Export(contentType);
        }

        private static X509Certificate2 CreateCertificate()
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new CertificateRequest(
                "CN=X509CertificateDataTests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        }
    }
}
