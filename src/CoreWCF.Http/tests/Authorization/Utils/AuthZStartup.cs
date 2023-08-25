// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Http.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;

namespace CoreWCF.Http.Tests.Authorization.Utils;

public class AuthZStartup<TSecuredService> where TSecuredService : class, ISecuredService
{
    public bool IsAuthenticated { get; set; }
    public List<string> ScopeClaimValues { get; set; } = new();

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<FakeJwtBearerAuthenticationHandlerOptions>(options =>
        {
            options.IsAuthenticated = IsAuthenticated;
            options.ScopeClaimValues = ScopeClaimValues;
        });

        services.AddAuthentication(AuthenticationScheme)
            .AddScheme<FakeJwtBearerAuthenticationHandlerOptions, FakeJwtBearerAuthenticationHandler>(
                AuthenticationScheme,
                options =>
                {
                    options.IsAuthenticated = IsAuthenticated;
                    options.ScopeClaimValues = ScopeClaimValues;
                });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationUtils.Policies.Write,
                policy => policy.RequireAuthenticatedUser()
                    .RequireClaim("scope", AuthorizationUtils.DefinedScopeValues.Write));
            options.AddPolicy(AuthorizationUtils.Policies.Read,
                policy => policy.RequireAuthenticatedUser()
                    .RequireClaim("scope", AuthorizationUtils.DefinedScopeValues.Read));
            options.DefaultPolicy =
                new AuthorizationPolicyBuilder(AuthenticationScheme)
                    .RequireClaim("scope", AuthorizationUtils.DefinedScopeValues.Read)
                    .Build();

        });
        services.AddServiceModelServices();
        services.AddTransient<TSecuredService>();

        OnPostConfigureServices(services);
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseServiceModel(builder =>
        {
            builder.AddService<TSecuredService>();
            builder.AddServiceEndpoint<TSecuredService, ISecuredService>(ConfigureBinding(), "/service.svc");
        });
    }

    protected virtual void OnPostConfigureServices(IServiceCollection services)
    {

    }

    protected virtual Binding ConfigureBinding()
    {
        return new BasicHttpBinding
        {
            Security = new BasicHttpSecurity
            {
                Mode = BasicHttpSecurityMode.TransportCredentialOnly,
                Transport = new HttpTransportSecurity
                {
                    ClientCredentialType = HttpClientCredentialType.InheritedFromHost
                }
            }
        };
    }

    protected virtual string AuthenticationScheme => FakeJwtBearerAuthenticationHandler.AuthenticationScheme;
}
