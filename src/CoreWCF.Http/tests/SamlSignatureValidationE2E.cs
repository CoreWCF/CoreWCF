// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET472
extern alias bcl;
#endif

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
using CoreWCFTokens = CoreWCF.IdentityModel.Tokens;
using CoreWCFSelectors = CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using MSamlTokens = Microsoft.IdentityModel.Tokens.Saml;
using WSFederationHttpBinding = System.ServiceModel.Federation.WSFederationHttpBinding;
using SigningCredentials = Microsoft.IdentityModel.Tokens.SigningCredentials;
using SecurityAlgorithms = Microsoft.IdentityModel.Tokens.SecurityAlgorithms;
#if NET472
using SamlAssertionKeyIdentifierClause = bcl::System.IdentityModel.Tokens.SamlAssertionKeyIdentifierClause;
#else
using SamlAssertionKeyIdentifierClause = System.IdentityModel.Tokens.SamlAssertionKeyIdentifierClause;
#endif
using Xunit;
using Xunit.Abstractions;

namespace SamlE2E
{
    public class SamlSignatureValidationE2ETests
    {
        private const string TestIssuer = "Test-STS";
        private const string TestSubjectName = "user@example.com";

        private readonly ITestOutputHelper _output;

        public SamlSignatureValidationE2ETests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestSamlEndToEndOverHttp()
        {
            List<string> firstChance = new List<string>();
            EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler =
                (s, e) => { lock (firstChance) firstChance.Add(e.Exception.GetType().FullName + ": " + e.Exception.Message); };
            AppDomain.CurrentDomain.FirstChanceException += handler;
            try
            {
                using X509Certificate2 stsCert = CreateSelfSignedCert("CN=Test-STS");
                using X509Certificate2 attackerCert = CreateSelfSignedCert("CN=Attacker");
                using X509Certificate2 transportCert = CreateSelfSignedCert("CN=localhost");

                int port = GetFreeTcpPort();
                Uri serviceAddress = new Uri($"https://localhost:{port}/SamlE2E");

                TestState.Reset(stsCert, serviceAddress, transportCert);

                using IWebHost host = BuildHost(port, transportCert);
                host.Start();

                // Positive case
                string validXml = BuildSignedAssertion(stsCert, stsCert, serviceAddress.AbsoluteUri);
                string echoed;
                try
                {
                    echoed = InvokeEcho(serviceAddress, validXml, stsCert, transportCert, "hello");
                }
                catch (Exception ex)
                {
                    _output.WriteLine("Positive case failed:\n" + FlattenExceptionMessages(ex));
                    _output.WriteLine("---- First-chance exceptions during positive case ----");
                    lock (firstChance) foreach (string s in firstChance) _output.WriteLine(s);
                    throw;
                }
                Assert.Equal("hello", echoed);
                Assert.True(TestState.TokenProviderInvocationCount > 0,
                    "Custom client SecurityTokenProvider was never invoked");

                int positiveInvocations = TestState.TokenProviderInvocationCount;
                lock (firstChance) firstChance.Clear();

                // Negative case
                string forgedXml = BuildForgedAssertion(stsCert, attackerCert, serviceAddress.AbsoluteUri);
                string negativeResult = null;
                Exception caught = null;
                try
                {
                    negativeResult = InvokeEcho(serviceAddress, forgedXml, stsCert, transportCert, "should not pass");
                }
                catch (Exception ex)
                {
                    caught = ex;
                }

                if (caught == null)
                {
                    _output.WriteLine($"!! Forged token was ACCEPTED by the server. Echo returned: {negativeResult ?? "<null>"}");
                    _output.WriteLine($"STS thumbprint: {stsCert.Thumbprint}");
                    _output.WriteLine($"Attacker thumbprint: {attackerCert.Thumbprint}");
                    _output.WriteLine("First-chance exceptions during negative case:");
                    lock (firstChance) foreach (string s in firstChance) _output.WriteLine(s);
                }

                Assert.NotNull(caught);
                Assert.True(TestState.TokenProviderInvocationCount > positiveInvocations,
                    "Custom client SecurityTokenProvider was not invoked for the negative case");

                string clientSideMessages = FlattenExceptionMessages(caught);
                string serverSideExceptions;
                lock (firstChance) serverSideExceptions = string.Join("\n", firstChance);

                Assert.True(
                    clientSideMessages.IndexOf("security", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Expected the wire fault to indicate a security failure. Actual:\n" + clientSideMessages);
                Assert.True(
                    serverSideExceptions.IndexOf("IDX10514", StringComparison.Ordinal) >= 0
                        || serverSideExceptions.IndexOf("SecurityTokenInvalidSignatureException", StringComparison.Ordinal) >= 0
                        || serverSideExceptions.IndexOf("SignatureVerificationFailed", StringComparison.Ordinal) >= 0
                        || serverSideExceptions.IndexOf("signature", StringComparison.OrdinalIgnoreCase) >= 0,
                    "Expected a signature-validation failure in the in-process exception chain. Actual:\n" + serverSideExceptions);
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }
        }

        private static IWebHost BuildHost(int port, X509Certificate2 transportCert)
        {
            return new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.Listen(IPAddress.Loopback, port, listenOptions =>
                    {
                        listenOptions.UseHttps(transportCert);
                    });
                })
                .ConfigureServices(services => services.AddServiceModelServices())
                .UseStartup<TestStartup>()
                .Build();
        }

        private static int GetFreeTcpPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static X509Certificate2 CreateSelfSignedCert(string subjectName)
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new CertificateRequest(
                subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection
                    {
                        new Oid("1.3.6.1.5.5.7.3.1"),
                        new Oid("1.3.6.1.5.5.7.3.2"),
                    },
                    critical: false));

            DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            DateTimeOffset notAfter = DateTimeOffset.UtcNow.AddDays(1);
            using X509Certificate2 ephemeral = request.CreateSelfSigned(notBefore, notAfter);
            byte[] pfx = ephemeral.Export(X509ContentType.Pfx);
            return new X509Certificate2(
                pfx, (string)null,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        }

        private static string BuildSignedAssertion(
            X509Certificate2 signingKeyCert, X509Certificate2 keyInfoCert, string audience)
        {
            DateTime now = DateTime.UtcNow;
            SigningCredentials signingCredentials = new SigningCredentials(
                new X509SecurityKey(signingKeyCert),
                SecurityAlgorithms.RsaSha256Signature,
                SecurityAlgorithms.Sha256Digest);

            MSamlTokens.SamlSubject subject = new MSamlTokens.SamlSubject(
                "http://schemas.xmlsoap.org/claims/upn", null, TestSubjectName);
            subject.ConfirmationMethods.Add("urn:oasis:names:tc:SAML:1.0:cm:bearer");

            MSamlTokens.SamlAttribute attribute = new MSamlTokens.SamlAttribute(
                "http://schemas.xmlsoap.org/claims", "EmailAddress", new[] { TestSubjectName });
            MSamlTokens.SamlAttributeStatement attrStatement =
                new MSamlTokens.SamlAttributeStatement(subject, new[] { attribute });

            MSamlTokens.SamlAudienceRestrictionCondition audienceCondition =
                new MSamlTokens.SamlAudienceRestrictionCondition(new Uri(audience));
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
                samlStatements: new MSamlTokens.SamlStatement[] { attrStatement })
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
            string xml = sb.ToString();

            if (!ReferenceEquals(signingKeyCert, keyInfoCert))
            {
                xml = ReplaceX509CertificateInKeyInfo(xml, keyInfoCert);
            }
            return xml;
        }

        private static string BuildForgedAssertion(
            X509Certificate2 trustedStsCert, X509Certificate2 attackerCert, string audience)
        {
            // Sign with attacker key, but rewrite KeyInfo to advertise the trusted STS cert.
            return BuildSignedAssertion(attackerCert, trustedStsCert, audience);
        }

        private static string ReplaceX509CertificateInKeyInfo(string xml, X509Certificate2 keyInfoCert)
        {
            const string xmldsig = "http://www.w3.org/2000/09/xmldsig#";
            XmlDocument doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(xml);
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("ds", xmldsig);
            XmlNode certNode = doc.SelectSingleNode(
                "//ds:Signature/ds:KeyInfo/ds:X509Data/ds:X509Certificate", nsmgr);
            if (certNode == null)
            {
                throw new InvalidOperationException(
                    "Could not locate ds:Signature/ds:KeyInfo/ds:X509Data/ds:X509Certificate.");
            }
            certNode.InnerText = Convert.ToBase64String(keyInfoCert.RawData);
            return doc.OuterXml;
        }

        private static string InvokeEcho(
            Uri serviceAddress, string assertionXml, X509Certificate2 expectedKeyInfoCert,
            X509Certificate2 transportCert, string text)
        {
            XmlDocument doc = new XmlDocument { PreserveWhitespace = true };
            doc.LoadXml(assertionXml);
            string assertionId = doc.DocumentElement.GetAttribute("AssertionID");
            System.IdentityModel.Tokens.SecurityKeyIdentifierClause skiClause =
                new SamlAssertionKeyIdentifierClause(assertionId);
            ReadOnlyCollection<System.IdentityModel.Policy.IAuthorizationPolicy> emptyPolicies =
                new ReadOnlyCollection<System.IdentityModel.Policy.IAuthorizationPolicy>(
                    new System.IdentityModel.Policy.IAuthorizationPolicy[0]);

            GenericXmlSecurityToken issuedToken = new GenericXmlSecurityToken(
                tokenXml: doc.DocumentElement,
                proofToken: null,
                effectiveTime: DateTime.UtcNow.AddMinutes(-1),
                expirationTime: DateTime.UtcNow.AddHours(1),
                internalTokenReference: skiClause,
                externalTokenReference: skiClause,
                authorizationPolicies: emptyPolicies);

            EndpointAddress endpoint = new EndpointAddress(serviceAddress);

            WSFederationHttpBinding clientBinding = BuildClientBinding();
            ChannelFactory<ClientContract.IEchoService> factory =
                new ChannelFactory<ClientContract.IEchoService>(clientBinding, endpoint);

            factory.Endpoint.EndpointBehaviors.Remove(typeof(ClientCredentials));
            factory.Endpoint.EndpointBehaviors.Add(new IssuedTokenClientCredentials(issuedToken));

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

        private static WSFederationHttpBinding BuildClientBinding()
        {
            WSTrustTokenParameters trustParameters = new WSTrustTokenParameters
            {
                IssuerAddress = new EndpointAddress(new Uri("https://localhost/unused-sts")),
                IssuerBinding = new WSHttpBinding(SecurityMode.Transport),
                KeyType = SecurityKeyType.BearerKey,
                TokenType = MSamlTokens.SamlConstants.OasisWssSamlTokenProfile11,
                MessageSecurityVersion = MessageSecurityVersion
                    .WSSecurity11WSTrust13WSSecureConversation13WSSecurityPolicy12BasicSecurityProfile10,
            };
            WSFederationHttpBinding binding = new WSFederationHttpBinding(trustParameters);
            binding.Security.Mode = SecurityMode.TransportWithMessageCredential;
            binding.Security.Message.EstablishSecurityContext = false;
            return binding;
        }

        private static string FlattenExceptionMessages(Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                sb.Append(e.GetType().FullName).Append(": ").AppendLine(e.Message);
            }
            return sb.ToString();
        }

        // ---- Static state shared between the test method and the in-process Startup ----

        internal static class TestState
        {
            private static int s_tokenProviderInvocations;

            public static X509Certificate2 StsCert { get; private set; }
            public static Uri ServiceAddress { get; private set; }
            public static X509Certificate2 TransportCert { get; private set; }

            public static int TokenProviderInvocationCount => Volatile.Read(ref s_tokenProviderInvocations);

            public static void Reset(X509Certificate2 stsCert, Uri serviceAddress, X509Certificate2 transportCert)
            {
                StsCert = stsCert;
                ServiceAddress = serviceAddress;
                TransportCert = transportCert;
                Volatile.Write(ref s_tokenProviderInvocations, 0);
            }

            public static void NoteTokenProviderInvocation() => Interlocked.Increment(ref s_tokenProviderInvocations);
        }

        // ---- ASP.NET Core Startup hosting the federated EchoService ----

        public class TestStartup
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
                        IdentityConfiguration identityConfiguration = host.Credentials.IdentityConfiguration;
                        identityConfiguration.AudienceRestriction.AllowedAudienceUris.Add(TestState.ServiceAddress);
                        identityConfiguration.CertificateValidationMode = CoreWCF.Security.X509CertificateValidationMode.None;
                        identityConfiguration.CertificateValidator = CoreWCFSelectors.X509CertificateValidator.None;
                        identityConfiguration.RevocationMode = X509RevocationMode.NoCheck;
                        identityConfiguration.SaveBootstrapContext = true;

                        CoreWCFTokens.ConfigurationBasedIssuerNameRegistry registry =
                            new CoreWCFTokens.ConfigurationBasedIssuerNameRegistry();
                        registry.AddTrustedIssuer(TestState.StsCert.Thumbprint, TestIssuer);
                        identityConfiguration.IssuerNameRegistry = registry;

                        List<CoreWCFTokens.SecurityToken> issuerTokens = new List<CoreWCFTokens.SecurityToken>
                        {
                            new CoreWCFTokens.X509SecurityToken(TestState.StsCert)
                        };
                        identityConfiguration.IssuerTokenResolver =
                            CoreWCFSelectors.SecurityTokenResolver.CreateDefaultSecurityTokenResolver(
                                issuerTokens.AsReadOnly(), false);
                    });
                });
            }
        }

        // ---- Custom client credentials that inject a pre-issued token ----

        private sealed class IssuedTokenClientCredentials : ClientCredentials
        {
            private readonly GenericXmlSecurityToken _token;

            public IssuedTokenClientCredentials(GenericXmlSecurityToken token)
            {
                _token = token ?? throw new ArgumentNullException(nameof(token));
                ServiceCertificate.SslCertificateAuthentication =
                    new X509ServiceCertificateAuthentication
                    {
                        CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None,
                        RevocationMode = X509RevocationMode.NoCheck,
                    };
            }

            private IssuedTokenClientCredentials(IssuedTokenClientCredentials other) : base(other)
            {
                _token = other._token;
            }

            protected override ClientCredentials CloneCore() => new IssuedTokenClientCredentials(this);

            public override System.IdentityModel.Selectors.SecurityTokenManager CreateSecurityTokenManager() =>
                new IssuedTokenClientCredentialsManager(this, _token);
        }

        private sealed class IssuedTokenClientCredentialsManager : ClientCredentialsSecurityTokenManager
        {
            private readonly GenericXmlSecurityToken _token;

            public IssuedTokenClientCredentialsManager(IssuedTokenClientCredentials credentials, GenericXmlSecurityToken token)
                : base(credentials)
            {
                _token = token;
            }

            public override System.IdentityModel.Selectors.SecurityTokenProvider CreateSecurityTokenProvider(SecurityTokenRequirement tokenRequirement)
            {
                if (tokenRequirement is InitiatorServiceModelSecurityTokenRequirement init
                    && init.TokenType == MSamlTokens.SamlConstants.OasisWssSamlTokenProfile11)
                {
                    return new FixedTokenProvider(_token);
                }
                return base.CreateSecurityTokenProvider(tokenRequirement);
            }
        }

        private sealed class FixedTokenProvider : System.IdentityModel.Selectors.SecurityTokenProvider
        {
            private readonly GenericXmlSecurityToken _token;
            public FixedTokenProvider(GenericXmlSecurityToken token) => _token = token;

            protected override System.IdentityModel.Tokens.SecurityToken GetTokenCore(TimeSpan timeout)
            {
                TestState.NoteTokenProviderInvocation();
                return _token;
            }
        }
    }
}
