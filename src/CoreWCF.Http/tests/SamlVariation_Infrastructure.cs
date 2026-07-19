// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET472
extern alias bcl;
#endif

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IdentityModel.Tokens;
using System.Net;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Federation;
using System.ServiceModel.Security;
using System.ServiceModel.Security.Tokens;
using System.Text;
using System.Threading;
using System.Xml;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MSamlTokens = Microsoft.IdentityModel.Tokens.Saml;
using WSFederationHttpBinding = System.ServiceModel.Federation.WSFederationHttpBinding;
using SigningCredentials = Microsoft.IdentityModel.Tokens.SigningCredentials;
using SecurityAlgorithms = Microsoft.IdentityModel.Tokens.SecurityAlgorithms;
#if NET472
using SamlAssertionKeyIdentifierClause = bcl::System.IdentityModel.Tokens.SamlAssertionKeyIdentifierClause;
using Saml2AssertionKeyIdentifierClause = bcl::System.IdentityModel.Tokens.Saml2AssertionKeyIdentifierClause;
#else
using SamlAssertionKeyIdentifierClause = System.IdentityModel.Tokens.SamlAssertionKeyIdentifierClause;
using Saml2AssertionKeyIdentifierClause = System.IdentityModel.Tokens.Saml2AssertionKeyIdentifierClause;
#endif
using Xunit;
using CoreWCFTokens = CoreWCF.IdentityModel.Tokens;
using CoreWCFSelectors = CoreWCF.IdentityModel.Selectors;

namespace SamlVariation
{
    /// <summary>
    /// Marker collection so all SAML variation tests run sequentially. The host fixture is
    /// shared across all classes in the collection so we have one Kestrel host serving all
    /// variation tests, avoiding contention on the static <see cref="SamlVariationServerState"/>.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public class SamlVariationCollection : ICollectionFixture<SamlVariationHostFixture>
    {
        public const string Name = "SAML Variation E2E";
    }

    public interface ISamlEchoFixture
    {
        Uri ServiceAddress { get; }
        X509Certificate2 StsCert { get; }
        void NoteTokenProviderInvocation();
    }

    public sealed class SamlVariationHostFixture : IDisposable, ISamlEchoFixture
    {
        public const string TestIssuer = "Test-STS";

        private readonly IWebHost _host;
        private int _tokenProviderInvocations;

        public X509Certificate2 StsCert { get; }
        public X509Certificate2 TransportCert { get; }
        public Uri ServiceAddress { get; }

        public int TokenProviderInvocations => Volatile.Read(ref _tokenProviderInvocations);

        public SamlVariationHostFixture()
        {
            StsCert = SamlTestCryptography.CreateSelfSignedCert("CN=Test-STS");
            TransportCert = SamlTestCryptography.CreateSelfSignedCert("CN=localhost");
            int port = SamlTestCryptography.GetFreeTcpPort();
            ServiceAddress = new Uri($"https://localhost:{port}/SamlE2E");

            SamlVariationServerState.Set(StsCert, ServiceAddress);

            _host = new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, port, listenOptions =>
                    {
                        listenOptions.UseHttps(TransportCert);
                    });
                })
                .ConfigureServices(services => services.AddServiceModelServices())
                .UseStartup<SamlVariationStartup>()
                .Build();

            _host.Start();
        }

        internal void NoteTokenProviderInvocation() => Interlocked.Increment(ref _tokenProviderInvocations);

        void ISamlEchoFixture.NoteTokenProviderInvocation() => NoteTokenProviderInvocation();

        public int SnapshotInvocations() => TokenProviderInvocations;

        public void Dispose()
        {
            try { _host.StopAsync().GetAwaiter().GetResult(); } catch { }
            try { _host.Dispose(); } catch { }
            try { StsCert.Dispose(); } catch { }
            try { TransportCert.Dispose(); } catch { }
        }
    }

    internal static class SamlVariationServerState
    {
        public static X509Certificate2 StsCert { get; private set; }
        public static Uri ServiceAddress { get; private set; }

        public static void Set(X509Certificate2 stsCert, Uri serviceAddress)
        {
            StsCert = stsCert;
            ServiceAddress = serviceAddress;
        }
    }

    public class SamlVariationStartup
    {
        public void ConfigureServices(IServiceCollection services) => services.AddServiceModelServices();

        public void Configure(IApplicationBuilder app)
        {
            CoreWCF.WS2007FederationHttpBinding wsFedBinding = new CoreWCF.WS2007FederationHttpBinding(
                CoreWCF.WSFederationHttpSecurityMode.TransportWithMessageCredential);
            wsFedBinding.Security.Message.EstablishSecurityContext = false;
            wsFedBinding.Security.Message.IssuedKeyType = CoreWCFTokens.SecurityKeyType.BearerKey;

            app.UseServiceModel(builder =>
            {
                builder.AddService<Services.EchoService>();
                builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(
                    wsFedBinding, "/SamlE2E");
                builder.ConfigureServiceHostBase<Services.EchoService>(host =>
                {
                    host.Credentials.IssuedTokenAuthentication.CertificateValidationMode =
                        CoreWCF.Security.X509CertificateValidationMode.None;
                    host.Credentials.IssuedTokenAuthentication.RevocationMode = X509RevocationMode.NoCheck;

                    host.Credentials.UseIdentityConfiguration = true;
                    IdentityConfiguration ic = host.Credentials.IdentityConfiguration;
                    ic.AudienceRestriction.AllowedAudienceUris.Add(SamlVariationServerState.ServiceAddress);
                    ic.CertificateValidationMode = CoreWCF.Security.X509CertificateValidationMode.None;
                    ic.CertificateValidator = CoreWCFSelectors.X509CertificateValidator.None;
                    ic.RevocationMode = X509RevocationMode.NoCheck;
                    ic.SaveBootstrapContext = true;

                    CoreWCFTokens.ConfigurationBasedIssuerNameRegistry registry =
                        new CoreWCFTokens.ConfigurationBasedIssuerNameRegistry();
                    registry.AddTrustedIssuer(SamlVariationServerState.StsCert.Thumbprint, SamlVariationHostFixture.TestIssuer);
                    ic.IssuerNameRegistry = registry;

                    List<CoreWCFTokens.SecurityToken> issuerTokens = new List<CoreWCFTokens.SecurityToken>
                    {
                        new CoreWCFTokens.X509SecurityToken(SamlVariationServerState.StsCert)
                    };
                    ic.IssuerTokenResolver =
                        CoreWCFSelectors.SecurityTokenResolver.CreateDefaultSecurityTokenResolver(
                            issuerTokens.AsReadOnly(), false);
                });
            });
        }
    }

    /// <summary>
    /// Captures FirstChanceException messages for the duration of a test.
    /// Use one instance per test so each test sees only its own exceptions.
    /// </summary>
    public sealed class FirstChanceCapture : IDisposable
    {
        private readonly List<string> _list = new List<string>();
        private readonly EventHandler<FirstChanceExceptionEventArgs> _handler;

        public FirstChanceCapture()
        {
            _handler = (s, e) =>
            {
                lock (_list) _list.Add(e.Exception.GetType().FullName + ": " + e.Exception.Message);
            };
            AppDomain.CurrentDomain.FirstChanceException += _handler;
        }

        public string Joined
        {
            get { lock (_list) return string.Join("\n", _list); }
        }

        public void Dispose() => AppDomain.CurrentDomain.FirstChanceException -= _handler;
    }

    public static class SamlTestCryptography
    {
        public static int GetFreeTcpPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public static X509Certificate2 CreateSelfSignedCert(string subjectName, int keySizeBits = 2048,
            DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
        {
            using RSA rsa = RSA.Create(keySizeBits);
            CertificateRequest request = new CertificateRequest(
                subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection
                    {
                        new Oid("1.3.6.1.5.5.7.3.1"),
                        new Oid("1.3.6.1.5.5.7.3.2"),
                    },
                    critical: false));
            request.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

            using X509Certificate2 ephemeral = request.CreateSelfSigned(
                notBefore ?? DateTimeOffset.UtcNow.AddMinutes(-5),
                notAfter ?? DateTimeOffset.UtcNow.AddDays(1));
            byte[] pfx = ephemeral.Export(X509ContentType.Pfx);
            return new X509Certificate2(
                pfx, (string)null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        }

        public static X509Certificate2 CreateSelfSignedEcdsaCert(string subjectName)
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            CertificateRequest request = new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256);
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
            using X509Certificate2 ephemeral = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
            byte[] pfx = ephemeral.Export(X509ContentType.Pfx);
            return new X509Certificate2(
                pfx, (string)null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        }
    }

    public enum KeyInfoStyle
    {
        X509Certificate,
        X509SubjectName,
        X509IssuerSerial,
        X509SKI,
        KeyName,
        WsseKeyIdentifierThumbprint,
        None,
    }

    public sealed class SamlAssertionBuilder
    {
        public string Issuer { get; set; } = SamlVariationHostFixture.TestIssuer;
        public string AssertionId { get; set; }
        public DateTime? IssueInstant { get; set; }
        public DateTime? NotBefore { get; set; }
        public DateTime? NotOnOrAfter { get; set; }

        // Each inner list is a single AudienceRestriction containing one or more Audience URIs.
        public List<List<string>> AudienceRestrictionBlocks { get; } = new List<List<string>>();

        public string SubjectName { get; set; } = "user@example.com";
        public string SubjectNameFormat { get; set; } = "http://schemas.xmlsoap.org/claims/upn";
        public List<string> ConfirmationMethods { get; } = new List<string>();

        // Statements: "attribute", "authn", "authzdecision". Default = single attribute.
        public List<string> Statements { get; } = new List<string>();

        public X509Certificate2 SigningCert { get; set; }
        public string SignatureAlgorithm { get; set; } = SecurityAlgorithms.RsaSha256Signature;
        public string DigestAlgorithm { get; set; } = SecurityAlgorithms.Sha256Digest;
        public bool IncludeSignature { get; set; } = true;

        public KeyInfoStyle KeyInfoStyle { get; set; } = KeyInfoStyle.X509Certificate;
        public X509Certificate2 KeyInfoCert { get; set; }

        public Action<XmlDocument> PostSignMutate { get; set; }

        public string Build()
        {
            DateTime now = IssueInstant ?? DateTime.UtcNow;
            DateTime nb = NotBefore ?? now.AddMinutes(-1);
            DateTime na = NotOnOrAfter ?? now.AddHours(1);

            if (ConfirmationMethods.Count == 0)
            {
                ConfirmationMethods.Add("urn:oasis:names:tc:SAML:1.0:cm:bearer");
            }

            MSamlTokens.SamlSubject subject = new MSamlTokens.SamlSubject(
                SubjectNameFormat, null, SubjectName);
            foreach (string m in ConfirmationMethods)
            {
                subject.ConfirmationMethods.Add(m);
            }

            List<MSamlTokens.SamlStatement> statements = new List<MSamlTokens.SamlStatement>();
            List<string> stmtKinds = Statements.Count == 0
                ? new List<string> { "attribute" }
                : new List<string>(Statements);
            foreach (string kind in stmtKinds)
            {
                if (kind.Equals("attribute", StringComparison.OrdinalIgnoreCase))
                {
                    MSamlTokens.SamlAttribute attribute = new MSamlTokens.SamlAttribute(
                        "http://schemas.xmlsoap.org/claims", "EmailAddress", new[] { SubjectName });
                    statements.Add(new MSamlTokens.SamlAttributeStatement(subject, new[] { attribute }));
                }
                else if (kind.Equals("authn", StringComparison.OrdinalIgnoreCase))
                {
                    statements.Add(new MSamlTokens.SamlAuthenticationStatement(
                        subject,
                        "urn:oasis:names:tc:SAML:1.0:am:password",
                        now,
                        dnsAddress: null,
                        ipAddress: null,
                        authorityBindings: null));
                }
                else if (kind.Equals("authzdecision", StringComparison.OrdinalIgnoreCase))
                {
                    // SamlAuthorizationDecisionStatement is not exposed by Microsoft.IdentityModel.Tokens.Saml in the
                    // version we depend on (no SamlAccessDecision enum). Recorded in the failure report.
                    throw new NotSupportedException("authzdecision statement not supported by current MS.IM SAML 1.1 surface.");
                }
                else
                {
                    throw new ArgumentException("Unknown statement kind: " + kind);
                }
            }

            List<MSamlTokens.SamlCondition> conditions = new List<MSamlTokens.SamlCondition>();
            foreach (List<string> block in AudienceRestrictionBlocks)
            {
                List<Uri> uris = new List<Uri>();
                foreach (string s in block)
                {
                    uris.Add(new Uri(s, UriKind.RelativeOrAbsolute));
                }
                conditions.Add(new MSamlTokens.SamlAudienceRestrictionCondition(uris));
            }

            MSamlTokens.SamlConditions samlConditions = new MSamlTokens.SamlConditions(nb, na, conditions);

            string id = AssertionId ?? "_" + Guid.NewGuid().ToString("N");

            MSamlTokens.SamlAssertion assertion = new MSamlTokens.SamlAssertion(
                id, Issuer, now, samlConditions, samlAdvice: null, samlStatements: statements);

            if (IncludeSignature)
            {
                X509Certificate2 actualSigningCert = SigningCert ?? throw new InvalidOperationException(
                    "SigningCert must be set when IncludeSignature is true.");
                Microsoft.IdentityModel.Tokens.SecurityKey signingKey = CreateSigningKey(actualSigningCert);
                assertion.SigningCredentials = new SigningCredentials(
                    signingKey, SignatureAlgorithm, DigestAlgorithm);
            }

            MSamlTokens.SamlSecurityToken samlToken = new MSamlTokens.SamlSecurityToken(assertion);
            MSamlTokens.SamlSecurityTokenHandler msHandler = new MSamlTokens.SamlSecurityTokenHandler();

            StringBuilder sb = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(sb, new XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                msHandler.WriteToken(writer, samlToken);
            }
            string xml = sb.ToString();

            XmlDocument doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(xml);

            if (IncludeSignature && KeyInfoStyle != KeyInfoStyle.X509Certificate)
            {
                X509Certificate2 advertised = KeyInfoCert ?? SigningCert;
                RewriteKeyInfo(doc, KeyInfoStyle, advertised);
            }
            else if (IncludeSignature && KeyInfoCert != null && !ReferenceEquals(KeyInfoCert, SigningCert))
            {
                ReplaceX509Certificate(doc, KeyInfoCert);
            }
            else if (IncludeSignature && KeyInfoStyle == KeyInfoStyle.X509Certificate && SigningCert != null && SigningCert.GetECDsaPrivateKey() != null)
            {
                // M.IdentityModel doesn't know to embed the cert in KeyInfo when the
                // SigningCredentials are an ECDsaSecurityKey (no X509 association); the
                // KeyInfo it writes contains only the raw KeyValue.  Force-rewrite to
                // X509Certificate so the server's IssuerTokenResolver can find the
                // trusted certificate.
                RewriteKeyInfo(doc, KeyInfoStyle.X509Certificate, KeyInfoCert ?? SigningCert);
            }

            PostSignMutate?.Invoke(doc);

            return doc.OuterXml;
        }

        private static Microsoft.IdentityModel.Tokens.SecurityKey CreateSigningKey(X509Certificate2 cert)
        {
            ECDsa ecdsa = cert.GetECDsaPrivateKey();
            if (ecdsa != null)
            {
                return new ECDsaSecurityKey(ecdsa);
            }
            return new X509SecurityKey(cert);
        }

        private static void ReplaceX509Certificate(XmlDocument doc, X509Certificate2 cert)
        {
            XmlNamespaceManager nsmgr = NsMgr(doc);
            XmlNode certNode = doc.SelectSingleNode(
                "//ds:Signature/ds:KeyInfo/ds:X509Data/ds:X509Certificate", nsmgr);
            if (certNode == null)
            {
                throw new InvalidOperationException("KeyInfo/X509Certificate node not found.");
            }
            certNode.InnerText = Convert.ToBase64String(cert.RawData);
        }

        private static void RewriteKeyInfo(XmlDocument doc, KeyInfoStyle style, X509Certificate2 cert)
        {
            XmlNamespaceManager nsmgr = NsMgr(doc);
            XmlElement keyInfo = (XmlElement)doc.SelectSingleNode("//ds:Signature/ds:KeyInfo", nsmgr);
            if (keyInfo == null)
            {
                throw new InvalidOperationException("KeyInfo element not found.");
            }
            // Wipe existing KeyInfo children
            while (keyInfo.FirstChild != null) keyInfo.RemoveChild(keyInfo.FirstChild);

            const string ds = "http://www.w3.org/2000/09/xmldsig#";

            switch (style)
            {
                case KeyInfoStyle.None:
                    keyInfo.ParentNode.RemoveChild(keyInfo);
                    return;

                case KeyInfoStyle.X509Certificate:
                {
                    XmlElement x509Data = doc.CreateElement("X509Data", ds);
                    XmlElement x509Cert = doc.CreateElement("X509Certificate", ds);
                    x509Cert.InnerText = Convert.ToBase64String(cert.RawData);
                    x509Data.AppendChild(x509Cert);
                    keyInfo.AppendChild(x509Data);
                    return;
                }

                case KeyInfoStyle.X509SubjectName:
                {
                    XmlElement x509Data = doc.CreateElement("X509Data", ds);
                    XmlElement subj = doc.CreateElement("X509SubjectName", ds);
                    subj.InnerText = cert.SubjectName.Name;
                    x509Data.AppendChild(subj);
                    keyInfo.AppendChild(x509Data);
                    return;
                }

                case KeyInfoStyle.X509IssuerSerial:
                {
                    XmlElement x509Data = doc.CreateElement("X509Data", ds);
                    XmlElement issuerSerial = doc.CreateElement("X509IssuerSerial", ds);
                    XmlElement issuerName = doc.CreateElement("X509IssuerName", ds);
                    issuerName.InnerText = cert.IssuerName.Name;
                    XmlElement serialNumber = doc.CreateElement("X509SerialNumber", ds);
                    serialNumber.InnerText = HexToDecimal(cert.SerialNumber);
                    issuerSerial.AppendChild(issuerName);
                    issuerSerial.AppendChild(serialNumber);
                    x509Data.AppendChild(issuerSerial);
                    keyInfo.AppendChild(x509Data);
                    return;
                }

                case KeyInfoStyle.X509SKI:
                {
                    XmlElement x509Data = doc.CreateElement("X509Data", ds);
                    XmlElement ski = doc.CreateElement("X509SKI", ds);
                    ski.InnerText = Convert.ToBase64String(GetSubjectKeyIdentifierBytes(cert));
                    x509Data.AppendChild(ski);
                    keyInfo.AppendChild(x509Data);
                    return;
                }

                case KeyInfoStyle.KeyName:
                {
                    XmlElement keyName = doc.CreateElement("KeyName", ds);
                    keyName.InnerText = cert.Thumbprint;
                    keyInfo.AppendChild(keyName);
                    return;
                }

                case KeyInfoStyle.WsseKeyIdentifierThumbprint:
                {
                    const string wsse = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
                    XmlElement str = doc.CreateElement("o", "SecurityTokenReference", wsse);
                    XmlElement kid = doc.CreateElement("o", "KeyIdentifier", wsse);
                    kid.SetAttribute("ValueType", "http://docs.oasis-open.org/wss/oasis-wss-soap-message-security-1.1#ThumbprintSHA1");
                    kid.SetAttribute("EncodingType", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary");
                    using (SHA1 sha = SHA1.Create())
                    {
                        kid.InnerText = Convert.ToBase64String(sha.ComputeHash(cert.RawData));
                    }
                    str.AppendChild(kid);
                    keyInfo.AppendChild(str);
                    return;
                }

                default:
                    throw new ArgumentOutOfRangeException(nameof(style));
            }
        }

        private static byte[] GetSubjectKeyIdentifierBytes(X509Certificate2 cert)
        {
            foreach (X509Extension ext in cert.Extensions)
            {
                if (ext is X509SubjectKeyIdentifierExtension ski)
                {
                    string hex = ski.SubjectKeyIdentifier;
                    byte[] bytes = new byte[hex.Length / 2];
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                    }
                    return bytes;
                }
            }
            // Compute one ourselves from the SubjectPublicKeyInfo SHA-1 (RFC 5280 method 1)
            byte[] spki = cert.GetPublicKey();
            using SHA1 sha = SHA1.Create();
            return sha.ComputeHash(spki);
        }

        private static string HexToDecimal(string hex)
        {
            // X509Certificate2.SerialNumber is hex (most-significant byte first). Convert to decimal string for X509SerialNumber.
            System.Numerics.BigInteger value = System.Numerics.BigInteger.Zero;
            foreach (char c in hex)
            {
                int digit = c <= '9' ? c - '0' : (char.ToUpperInvariant(c) - 'A' + 10);
                value = value * 16 + digit;
            }
            return value.ToString();
        }

        internal static XmlNamespaceManager NsMgr(XmlDocument doc)
        {
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
            nsmgr.AddNamespace("saml", "urn:oasis:names:tc:SAML:1.0:assertion");
            return nsmgr;
        }
    }

    public static class SamlEchoClient
    {
        public static string InvokeEcho(SamlVariationHostFixture fixture, string assertionXml, string text)
            => InvokeEcho((ISamlEchoFixture)fixture, assertionXml, text);

        public static string InvokeEcho(ISamlEchoFixture fixture, string assertionXml, string text)
            => InvokeEcho(fixture, assertionXml, text, MSamlTokens.SamlConstants.OasisWssSamlTokenProfile11);

        public static string InvokeEcho(ISamlEchoFixture fixture, string assertionXml, string text, string tokenType)
        {
            XmlDocument doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(assertionXml);

            string assertionId;
            System.IdentityModel.Tokens.SecurityKeyIdentifierClause skiClause;
            if (string.Equals(tokenType, Microsoft.IdentityModel.Tokens.Saml2.Saml2Constants.OasisWssSaml2TokenProfile11, StringComparison.Ordinal))
            {
                assertionId = doc.DocumentElement.GetAttribute("ID");
                skiClause = new Saml2AssertionKeyIdentifierClause(assertionId);
            }
            else
            {
                assertionId = doc.DocumentElement.GetAttribute("AssertionID");
                skiClause = new SamlAssertionKeyIdentifierClause(assertionId);
            }
            ReadOnlyCollection<System.IdentityModel.Policy.IAuthorizationPolicy> emptyPolicies =
                new ReadOnlyCollection<System.IdentityModel.Policy.IAuthorizationPolicy>(
                    Array.Empty<System.IdentityModel.Policy.IAuthorizationPolicy>());

            GenericXmlSecurityToken issuedToken = new GenericXmlSecurityToken(
                tokenXml: doc.DocumentElement,
                proofToken: null,
                effectiveTime: DateTime.UtcNow.AddMinutes(-1),
                expirationTime: DateTime.UtcNow.AddHours(1),
                internalTokenReference: skiClause,
                externalTokenReference: skiClause,
                authorizationPolicies: emptyPolicies);

            EndpointAddress endpoint = new EndpointAddress(fixture.ServiceAddress);
            WSFederationHttpBinding clientBinding = BuildClientBinding(tokenType);
            ChannelFactory<ClientContract.IEchoService> factory =
                new ChannelFactory<ClientContract.IEchoService>(clientBinding, endpoint);

            factory.Endpoint.EndpointBehaviors.Remove(typeof(ClientCredentials));
            factory.Endpoint.EndpointBehaviors.Add(new InjectingClientCredentials(issuedToken, fixture, tokenType));

            ClientContract.IEchoService channel = null;
            try
            {
                channel = factory.CreateChannel();
                ((IClientChannel)channel).Open();
                string result = channel.EchoString(text);
                ((IClientChannel)channel).Close();
                factory.Close();
                return result;
            }
            catch
            {
                try { ((IClientChannel)channel)?.Abort(); } catch { }
                try { factory.Abort(); } catch { }
                throw;
            }
        }

        private static WSFederationHttpBinding BuildClientBinding(string tokenType)
        {
            WSTrustTokenParameters trustParameters = new WSTrustTokenParameters
            {
                IssuerAddress = new EndpointAddress(new Uri("https://localhost/unused-sts")),
                IssuerBinding = new WSHttpBinding(System.ServiceModel.SecurityMode.Transport),
                KeyType = SecurityKeyType.BearerKey,
                TokenType = tokenType,
                MessageSecurityVersion = MessageSecurityVersion
                    .WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10,
            };
            WSFederationHttpBinding binding = new WSFederationHttpBinding(trustParameters);
            binding.Security.Mode = System.ServiceModel.SecurityMode.TransportWithMessageCredential;
            binding.Security.Message.EstablishSecurityContext = false;
            return binding;
        }

        public static string FlattenExceptions(Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                sb.Append(e.GetType().FullName).Append(": ").AppendLine(e.Message);
            }
            return sb.ToString();
        }

        private sealed class InjectingClientCredentials : ClientCredentials
        {
            private readonly GenericXmlSecurityToken _token;
            private readonly ISamlEchoFixture _fixture;
            private readonly string _tokenType;

            public InjectingClientCredentials(GenericXmlSecurityToken token, ISamlEchoFixture fixture, string tokenType)
            {
                _token = token;
                _fixture = fixture;
                _tokenType = tokenType;
                ServiceCertificate.SslCertificateAuthentication =
                    new X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None,
                        RevocationMode = X509RevocationMode.NoCheck,
                    };
            }

            private InjectingClientCredentials(InjectingClientCredentials other) : base(other)
            {
                _token = other._token;
                _fixture = other._fixture;
                _tokenType = other._tokenType;
            }

            protected override ClientCredentials CloneCore() => new InjectingClientCredentials(this);

            public override System.IdentityModel.Selectors.SecurityTokenManager CreateSecurityTokenManager() =>
                new InjectingSecurityTokenManager(this, _token, _fixture, _tokenType);
        }

        private sealed class InjectingSecurityTokenManager : ClientCredentialsSecurityTokenManager
        {
            private readonly GenericXmlSecurityToken _token;
            private readonly ISamlEchoFixture _fixture;
            private readonly string _tokenType;

            public InjectingSecurityTokenManager(InjectingClientCredentials creds,
                GenericXmlSecurityToken token, ISamlEchoFixture fixture, string tokenType)
                : base(creds)
            {
                _token = token;
                _fixture = fixture;
                _tokenType = tokenType;
            }

            public override System.IdentityModel.Selectors.SecurityTokenProvider CreateSecurityTokenProvider(
                System.IdentityModel.Selectors.SecurityTokenRequirement tokenRequirement)
            {
                if (tokenRequirement is InitiatorServiceModelSecurityTokenRequirement init
                    && init.TokenType == _tokenType)
                {
                    return new FixedTokenProvider(_token, _fixture);
                }
                return base.CreateSecurityTokenProvider(tokenRequirement);
            }
        }

        private sealed class FixedTokenProvider : System.IdentityModel.Selectors.SecurityTokenProvider
        {
            private readonly GenericXmlSecurityToken _token;
            private readonly ISamlEchoFixture _fixture;

            public FixedTokenProvider(GenericXmlSecurityToken token, ISamlEchoFixture fixture)
            {
                _token = token;
                _fixture = fixture;
            }

            protected override System.IdentityModel.Tokens.SecurityToken GetTokenCore(TimeSpan timeout)
            {
                _fixture.NoteTokenProviderInvocation();
                return _token;
            }
        }
    }
}
