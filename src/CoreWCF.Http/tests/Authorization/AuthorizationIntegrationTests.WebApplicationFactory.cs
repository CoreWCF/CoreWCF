﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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

public partial class AuthorizationIntegrationTests
{
    private class AuthorizationWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        public List<string> ScopeClaimValues { get; set; } = new();
        public bool IsAuthenticated { get; set; } = false;

        public AuthorizationServiceHolder AuthorizationServiceHolder { get; private set; } = new();
        public AuthenticationServiceHolder AuthenticationServiceHolder { get; private set; } = new();
        public SecuredServiceHolder SecuredServiceHolder { get; private set; } = new();

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
                    options.ScopeClaimValues = ScopeClaimValues;
                });

                services.AddAuthentication(FakeJwtBearerAuthenticationHandler.AuthenticationScheme)
                    .AddScheme<FakeJwtBearerAuthenticationHandlerOptions, FakeJwtBearerAuthenticationHandler>(FakeJwtBearerAuthenticationHandler.AuthenticationScheme,
                        options =>
                        {
                            options.IsAuthenticated = IsAuthenticated;
                            options.ScopeClaimValues = ScopeClaimValues;
                        });
                services.AddSingleton(_ => SecuredServiceHolder);
                services.AddTransient<SecuredService>();

                services.AddSingleton(_ => AuthorizationServiceHolder);
                services.AddTransient<DefaultAuthorizationService>();
                services.AddTransient<IAuthorizationService, AuthorizationServiceInterceptor>();

                services.AddSingleton(_ => AuthenticationServiceHolder);
                services.AddScoped<AuthenticationService>();
                services.AddScoped<IAuthenticationService, AuthenticationServiceInterceptor>();
            });
        }

        private static void SetSelfHostedContentRoot()
        {
            var contentRoot = Directory.GetCurrentDirectory();
            var assemblyName = typeof(AuthorizationWebApplicationFactory<TStartup>).Assembly.GetName().Name;
            var settingSuffix = assemblyName.ToUpperInvariant().Replace(".", "_");
            var settingName = $"ASPNETCORE_TEST_CONTENTROOT_{settingSuffix}";
            Environment.SetEnvironmentVariable(settingName, contentRoot);
        }
    }
}
