// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Configuration;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens.Saml;
using Microsoft.IdentityModel.Tokens.Saml2;
using Xunit;
using Xunit.Abstractions;

namespace WSFed
{
    public class SimpleWSFedBinding
    {
        private readonly ITestOutputHelper _output;

        public SimpleWSFedBinding(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory(Skip = "This require ADFS server set up to run.")]
        [InlineData(Saml2Constants.OasisWssSaml2TokenProfile11, false, true)]
        [InlineData(SamlConstants.OasisWssSamlTokenProfile11, false, true)]
        [InlineData(Saml2Constants.OasisWssSaml2TokenProfile11, true, true)]
        [InlineData(SamlConstants.OasisWssSamlTokenProfile11, true, true)]
        [InlineData(SamlConstants.OasisWssSamlTokenProfile11, false, false)]
        [InlineData(SamlConstants.OasisWssSamlTokenProfile11, true, false)]
        public void WSFedHttpRequestReplyEchoString(string tokenType, bool isToEstablishSecurityContext, bool isUserIdentity)
        {
            string testString = new string('a', 3000);
            IWebHost host;
            if(!isToEstablishSecurityContext)
            {
                host = isUserIdentity ? ServiceHelper.CreateHttpsWebHostBuilder<WSFedNoEstablishSecurityContextWithUserIdentity>(_output).Build()
                    : ServiceHelper.CreateHttpsWebHostBuilder<WSFedNoEstablishSecurityContextNoUserIdentity>(_output).Build();
            }
            else
            {
                host = isUserIdentity ? ServiceHelper.CreateHttpsWebHostBuilder<WSFedEstablishSecurityContextWithUserIdentity>(_output).Build()
                    : ServiceHelper.CreateHttpsWebHostBuilder<WSFedEstablishSecurityContextWithNoUserIdentity>(_output).Build();
            }
            using (host)
            {
                host.Start();
                var issuerAddress = new System.ServiceModel.EndpointAddress("https://youradserver/adfs/services/trust/13/usernamemixed");
                var issuerBinding = new System.ServiceModel.WSHttpBinding(System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                issuerBinding.Security.Message.EstablishSecurityContext = false;
                issuerBinding.Security.Message.NegotiateServiceCredential = false;
                issuerBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                // issuerBinding.Security.Transport.ClientCredentialType = System.ServiceModel.HttpClientCredentialType.Basic;
                System.ServiceModel.Federation.WSFederationHttpBinding federationBinding =
                    new System.ServiceModel.Federation.WSFederationHttpBinding(
                       new System.ServiceModel.Federation.WSTrustTokenParameters
                       {
                           IssuerAddress = issuerAddress,
                           IssuerBinding = issuerBinding,
                           KeyType = System.IdentityModel.Tokens.SecurityKeyType.BearerKey,
                           TokenType = tokenType,

                       }); ;

                federationBinding.Security.Message.EstablishSecurityContext = isToEstablishSecurityContext;
                var endpoint = "https://corewcfserver:8443/wsFedHttp";
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(federationBinding, new System.ServiceModel.EndpointAddress(endpoint));
                factory.Credentials.UserName.UserName = "yourusername";
                factory.Credentials.UserName.Password = "yourpassword";
                factory.Credentials.ClientCertificate.SetCertificate(StoreLocation.CurrentUser,
               StoreName.Root, X509FindType.FindByThumbprint,
               "bcb1467943780255eac5ba5864fdbdf655afc787");
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                var serviceProxy = factory.CreateChannel();
                System.ServiceModel.ICommunicationObject commObj = (System.ServiceModel.ICommunicationObject)serviceProxy;
                commObj.Open();
                string result = serviceProxy.EchoString(testString);
                Assert.Equal(testString, result);
                ((System.ServiceModel.ICommunicationObject)serviceProxy).Close();
                factory.Close();

            }
        }

        internal class WSFedNoEstablishSecurityContextNoUserIdentity : StartupWSHttpFedBase
        {
            public WSFedNoEstablishSecurityContextNoUserIdentity() : base(false, false)
            {

            }
        }

        internal class WSFedEstablishSecurityContextWithUserIdentity : StartupWSHttpFedBase
        {
            public WSFedEstablishSecurityContextWithUserIdentity() : base(true, true)
            {

            }
        }

        internal class WSFedEstablishSecurityContextWithNoUserIdentity : StartupWSHttpFedBase
        {
            public WSFedEstablishSecurityContextWithNoUserIdentity() : base(true, false)
            {

            }
        }

        internal class WSFedNoEstablishSecurityContextWithUserIdentity : StartupWSHttpFedBase
        {
            public WSFedNoEstablishSecurityContextWithUserIdentity() : base(false, true)
            {

            }
        }

        internal class StartupWSHttpFedBase
        {
            private bool _isSecurityContext = false;
            private bool _isUserIdentityConfig = false;
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }
            public StartupWSHttpFedBase(bool isSecurityContext, bool isUserIdentityConfig)
            {
                _isSecurityContext = isSecurityContext;
                _isUserIdentityConfig = isUserIdentityConfig;
            }

            public void Configure(IApplicationBuilder app)
            {

                WS2007FederationHttpBinding wsFedBinding = new WS2007FederationHttpBinding(WSFederationHttpSecurityMode.TransportWithMessageCredential);
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(wsFedBinding, "/wsFedHttp");
                    builder.ConfigureServiceHostBase<Services.EchoService>(host => ChangeHostBehavior(host, wsFedBinding));
                });
            }

            public void ChangeHostBehavior(ServiceHostBase host, WS2007FederationHttpBinding wsHttpFed)
            {
                wsHttpFed.Security.Message.IssuerAddress = new EndpointAddress("https://youradserver/adfs/services/trust/13/usernamemixed");
                wsHttpFed.Security.Message.IssuerMetadataAddress = new EndpointAddress("https://youradserver/adfs/services/trust/mex");
                wsHttpFed.Security.Message.EstablishSecurityContext = _isSecurityContext;
                wsHttpFed.Security.Message.IssuedKeyType = SecurityKeyType.BearerKey;
                ServiceBehaviorAttribute sb = new ServiceBehaviorAttribute();
                host.Credentials.ServiceCertificate.SetCertificate(StoreLocation.CurrentUser,
                        StoreName.Root, X509FindType.FindByThumbprint,
                        "bcb1467943780255eac5ba5864fdbdf655afc787");
                host.Credentials.ClientCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
                host.Credentials.UseIdentityConfiguration = _isUserIdentityConfig;
                // host.Credentials.ClientCertificate. = X509RevocationMode.NoCheck;
                IdentityConfiguration identityConfiguration = host.Credentials.IdentityConfiguration;
                identityConfiguration.AudienceRestriction.AllowedAudienceUris.Add(new Uri("https://corewcfserver:8443/wsFedHttp"));
                identityConfiguration.CertificateValidationMode = X509CertificateValidationMode.None;
                identityConfiguration.RevocationMode = X509RevocationMode.NoCheck;
                ConfigurationBasedIssuerNameRegistry configurationBasedIssuerNameRegistry = new ConfigurationBasedIssuerNameRegistry();
                configurationBasedIssuerNameRegistry.AddTrustedIssuer("C8A9BB79679B901ACEB4F36C7EC35AECC861838C".ToLower(), "http://youradserver/adfs/services/trust");
                identityConfiguration.IssuerNameRegistry = configurationBasedIssuerNameRegistry;
                identityConfiguration.SaveBootstrapContext = true;
                host.Credentials.IdentityConfiguration = identityConfiguration;
            }
        }
    }
}
