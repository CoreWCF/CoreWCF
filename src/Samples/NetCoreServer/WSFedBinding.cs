// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Configuration;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace NetCoreServer
{
    public class WSFedBinding
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {

            WS2007FederationHttpBinding wsFedBinding = new WS2007FederationHttpBinding(WSFederationHttpSecurityMode.TransportWithMessageCredential);
            app.UseServiceModel(builder =>
            {
                builder.AddService<EchoService>();
                builder.AddServiceEndpoint<EchoService, Contract.IEchoService>(wsFedBinding, "/wsFedHttp");
                builder.ConfigureServiceHostBase<EchoService>(host => ChangeHostBehavior(host, wsFedBinding));
            });
        }

        public void ChangeHostBehavior(ServiceHostBase host, WS2007FederationHttpBinding wsHttpFed)
        {

            wsHttpFed.Security.Message.IssuerAddress = new EndpointAddress("https://yourownadfsserver/adfs/services/trust/13/usernamemixed");
            wsHttpFed.Security.Message.IssuerMetadataAddress = new EndpointAddress("https://yourownadfsserver/adfs/services/trust/mex");
            wsHttpFed.Security.Message.EstablishSecurityContext = false;
            wsHttpFed.Security.Message.IssuedKeyType = SecurityKeyType.BearerKey;
            ServiceBehaviorAttribute sb = new ServiceBehaviorAttribute();
            host.Credentials.ServiceCertificate.SetCertificate(StoreLocation.CurrentUser,
                    StoreName.Root, X509FindType.FindByThumbprint,
                    "bcb1467943780255eac5ba5864fdbdf655afc787");
            host.Credentials.ClientCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
            host.Credentials.UseIdentityConfiguration = true;
            // host.Credentials.ClientCertificate. = X509RevocationMode.NoCheck;
            IdentityConfiguration identityConfiguration = host.Credentials.IdentityConfiguration;
            identityConfiguration.AudienceRestriction.AllowedAudienceUris.Add(new Uri("https://yourservice.mscore.local:8443/wsFedHttp"));
            identityConfiguration.CertificateValidationMode = X509CertificateValidationMode.None;
            identityConfiguration.RevocationMode = X509RevocationMode.NoCheck;
            ConfigurationBasedIssuerNameRegistry configurationBasedIssuerNameRegistry = new ConfigurationBasedIssuerNameRegistry();
            configurationBasedIssuerNameRegistry.AddTrustedIssuer("C8A9BB79679B901ACEB4F36C7EC35AECC861838C".ToLower(), "BirojAD.mscore.local");
            identityConfiguration.IssuerNameRegistry = configurationBasedIssuerNameRegistry;
            host.Credentials.IdentityConfiguration = identityConfiguration;
        }
 
    }
}
