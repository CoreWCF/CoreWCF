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
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization;

/// <summary>
/// Tests for class-based service contracts (where the service contract is defined on the class itself,
/// not as a separate interface). This ensures authorization policies work correctly with
/// AlwaysUseAuthorizationPolicySupport = true.
/// </summary>
public class ClassBasedServiceContractTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public ClassBasedServiceContractTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ClassBasedServiceContract_WithAlwaysUseAuthorizationPolicySupport_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<ClassBasedWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IClassBasedSecuredServiceContract>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            IClassBasedSecuredServiceContract channel = factory.CreateChannel();
            string result = channel.Echo(TestString);
            Assert.Equal(TestString, result);
        }
    }

    [Fact]
    public void ClassBasedServiceContract_WithAlwaysUseAuthorizationPolicySupport_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<ClassBasedWithUnauthenticatedUserStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IClassBasedSecuredServiceContract>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            IClassBasedSecuredServiceContract channel = factory.CreateChannel();
            Assert.Throws<MessageSecurityException>(() => channel.Echo(TestString));
        }
    }

    [Fact]
    public void ClassBasedServiceContract_WithAlwaysUseAuthorizationPolicySupport_AuthenticatedUser_MissingScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<ClassBasedWithAuthenticatedUserButMissingScopeValuesStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IClassBasedSecuredServiceContract>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            IClassBasedSecuredServiceContract channel = factory.CreateChannel();
            Assert.Throws<SecurityAccessDeniedException>(() => channel.Echo(TestString));
        }
    }

    [Fact]
    public void ClassBasedServiceContract_WithClassLevelAuthorization_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<ClassBasedWithClassLevelAuthorizationStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IClassBasedSecuredServiceWithClassLevelAuthContract>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            IClassBasedSecuredServiceWithClassLevelAuthContract channel = factory.CreateChannel();
            string result = channel.Echo(TestString);
            Assert.Equal(TestString, result);
        }
    }

    // Client-side interface for class-based service contract
    [System.ServiceModel.ServiceContract]
    public interface IClassBasedSecuredServiceContract
    {
        [System.ServiceModel.OperationContract]
        string Echo(string text);
    }

    // Client-side interface for class-based service contract with class-level authorization
    [System.ServiceModel.ServiceContract]
    public interface IClassBasedSecuredServiceWithClassLevelAuthContract
    {
        [System.ServiceModel.OperationContract]
        string Echo(string text);
    }

    // Service implementation with contract defined on the class itself
    [ServiceContract]
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    internal class ClassBasedSecuredService
    {
        [OperationContract]
        [Authorize(Policy = AuthorizationUtils.Policies.Read)]
        public string Echo(string text) => text;
    }

    // Service implementation with class-level authorization
    [ServiceContract]
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    [Authorize(Policy = AuthorizationUtils.Policies.Read)]
    internal class ClassBasedSecuredServiceWithClassLevelAuth
    {
        [OperationContract]
        public string Echo(string text) => text;
    }

    private class ClassBasedAuthZStartup<TService> where TService : class
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
            });
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseServiceModel(builder =>
            {
                builder.AddService<TService>();
                builder.AddServiceEndpoint<TService, TService>(ConfigureBinding(), "/service.svc");
            });
        }

        protected virtual Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.InheritedFromHost;
            basicHttpBinding.Security.Transport.AlwaysUseAuthorizationPolicySupport = true;
            return basicHttpBinding;
        }
    }

    private class ClassBasedWithAuthenticatedUserAndRequiredScopeValuesStartup : ClassBasedAuthZStartup<ClassBasedSecuredService>
    {
        public ClassBasedWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }
    }

    private class ClassBasedWithUnauthenticatedUserStartup : ClassBasedAuthZStartup<ClassBasedSecuredService>
    {
        public ClassBasedWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class ClassBasedWithAuthenticatedUserButMissingScopeValuesStartup : ClassBasedAuthZStartup<ClassBasedSecuredService>
    {
        public ClassBasedWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }
    }

    private class ClassBasedWithClassLevelAuthorizationStartup : ClassBasedAuthZStartup<ClassBasedSecuredServiceWithClassLevelAuth>
    {
        public ClassBasedWithClassLevelAuthorizationStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }
    }
}
