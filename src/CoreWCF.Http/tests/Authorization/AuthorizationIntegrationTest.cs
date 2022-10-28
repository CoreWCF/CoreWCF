// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Http.Tests.Authorization;

public class AuthorizationIntegrationTest<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
{
    public string DefaultScopeClaimValue { get; set; } = DefinedScopes.Read;
    public bool IsAuthenticated { get; set; } = false;

    public IAuthorizationService AuthorizationService { get; private set; }
    public IAuthenticationService AuthenticationService { get; private set; }

    protected override TestServer CreateServer(IWebHostBuilder builder)
    {
        var addresses = new ServerAddressesFeature();
        addresses.Addresses.Add("http://localhost/");
        addresses.Addresses.Add("https://localhost/");

        var features = new FeatureCollection();
        features.Set<IServerAddressesFeature>(addresses);

        var server = new TestServer(builder, features);
        return server;
    }

    protected override IWebHostBuilder CreateWebHostBuilder()
    {
        SetSelfHostedContentRoot();

        return ServiceHelper.CreateWebHostBuilder<TStartup>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.Configure<FakeJwtBearerAuthenticationHandlerOptions>(options =>
            {
                options.IsAuthenticated = IsAuthenticated;
                options.DefaultScopeClaimValue = DefaultScopeClaimValue;
            });

            services.AddAuthentication("Bearer")
                .AddScheme<FakeJwtBearerAuthenticationHandlerOptions, FakeJwtBearerAuthenticationHandler>("Bearer",
                    options =>
                    {
                        options.IsAuthenticated = IsAuthenticated;
                        options.DefaultScopeClaimValue = DefaultScopeClaimValue;
                    });

            services.AddTransient<DefaultAuthorizationService>();
            services.AddTransient(provider =>
            {
                AuthorizationService = new AuthorizationServiceInterceptor(provider.GetRequiredService<DefaultAuthorizationService>());
                return AuthorizationService;
            });
            services.AddScoped<AuthenticationService>();
            services.AddScoped(provider =>
            {
                AuthenticationService =
                    new AuthenticationServiceInterceptor(provider.GetRequiredService<AuthenticationService>());
                return AuthenticationService;
            });
        });
    }

    private static void SetSelfHostedContentRoot()
    {
        var contentRoot = Directory.GetCurrentDirectory();
        var assemblyName = typeof(AuthorizationIntegrationTest<TStartup>).Assembly.GetName().Name;
        var settingSuffix = assemblyName.ToUpperInvariant().Replace(".", "_");
        var settingName = $"ASPNETCORE_TEST_CONTENTROOT_{settingSuffix}";
        Environment.SetEnvironmentVariable(settingName, contentRoot);
    }
}
