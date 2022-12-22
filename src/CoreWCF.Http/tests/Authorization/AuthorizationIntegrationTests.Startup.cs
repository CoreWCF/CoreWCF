// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Http.Tests.Authorization;

public partial class AuthorizationIntegrationTests
{
    private class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                options.AddPolicy(Policies.Write,
                    policy => policy.RequireClaim("scope", DefinedScopeValues.Write));
                options.AddPolicy(Policies.Read,
                    policy => policy.RequireClaim("scope", DefinedScopeValues.Read));
            });
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<SecuredService>();
                builder.AddServiceEndpoint<SecuredService, ISecuredService>(
                    new BasicHttpBinding
                    {
                        Security = new BasicHttpSecurity
                        {
                            Mode = BasicHttpSecurityMode.TransportCredentialOnly,
                            Transport = new HttpTransportSecurity
                            {
                                ClientCredentialType = HttpClientCredentialType.InheritedFromHost
                            }
                        }
                    }, "/BasicWcfService/basichttp.svc");
                builder.AddServiceEndpoint<SecuredService, ISecuredService>(
                    new BasicHttpBinding
                    {
                        Security = new BasicHttpSecurity
                        {
                            Mode = BasicHttpSecurityMode.Transport,
                            Transport = new HttpTransportSecurity
                            {
                                ClientCredentialType = HttpClientCredentialType.InheritedFromHost
                            }
                        }
                    }, "/BasicWcfService/basichttp.svc");
            });
        }
    }
}
