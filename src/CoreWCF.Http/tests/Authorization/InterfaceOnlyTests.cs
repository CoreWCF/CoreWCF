// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ServiceModel.Security;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Http.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization;

public class InterfaceOnlyTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public InterfaceOnlyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void InterfaceOnly_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<InterfaceOnlyWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            ISecuredService channel = factory.CreateChannel();
            string result = channel.Echo(TestString);
            Assert.Equal(TestString, result);
        }
    }

    [Fact]
    public void InterfaceOnly_UnauthenticatedUser_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<InterfaceOnlyWithUnauthenticatedUserStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<MessageSecurityException>(() => channel.Echo(TestString));
        }
    }

    [Fact]
    public void InterfaceOnly_AuthenticatedUser_MissingScopeValues_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<InterfaceOnlyWithAuthenticatedUserButMissingScopeValuesStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<SecurityAccessDeniedException>(() => channel.Echo(TestString));
        }
    }

    private class InterfaceOnlyAuthZStartup<TSecuredService> where TSecuredService : class, ISecuredService
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

            services.AddAuthentication(FakeJwtBearerAuthenticationHandler.AuthenticationScheme)
                .AddScheme<FakeJwtBearerAuthenticationHandlerOptions, FakeJwtBearerAuthenticationHandler>(
                    FakeJwtBearerAuthenticationHandler.AuthenticationScheme,
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
                    new AuthorizationPolicyBuilder(FakeJwtBearerAuthenticationHandler.AuthenticationScheme)
                        .RequireClaim("scope", AuthorizationUtils.DefinedScopeValues.Read)
                        .Build();

            });
            services.AddServiceModelServices();
            services.AddTransient<ISecuredService, TSecuredService>();

            OnPostConfigureServices(services);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<ISecuredService>();
                builder.AddServiceEndpoint<ISecuredService, ISecuredService>(ConfigureBinding(), "/service.svc");
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
    }

    private class InterfaceOnlyWithAuthenticatedUserAndRequiredScopeValuesStartup : InterfaceOnlyAuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public InterfaceOnlyWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }
    }

    private class InterfaceOnlyWithUnauthenticatedUserStartup : InterfaceOnlyAuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public InterfaceOnlyWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class InterfaceOnlyWithAuthenticatedUserButMissingScopeValuesStartup : InterfaceOnlyAuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public InterfaceOnlyWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }
    }
}
