// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ServiceModel.Security;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Http.Tests.Authorization.Utils;
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
/// Tests authorization with class-based service contracts (where [ServiceContract] is defined on the class itself,
/// not as a separate interface). This ensures the fix for BuildAuthorizeData works correctly when 
/// operation.OperationMethod.DeclaringType is not an interface.
/// </summary>
public class ClassBasedServiceContractAuthorizationTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public ClassBasedServiceContractAuthorizationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [SkipOnGeneratedOperationInvokerTheory]
    [InlineData(typeof(ClassBasedAuthenticatedUserWithScopeStartup))]
    public void ClassBasedServiceContract_WithAlwaysUseAuthorizationPolicySupport_AuthenticatedUser_Test(Type startupType)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding binding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IClassBasedServiceClient>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            IClassBasedServiceClient channel = factory.CreateChannel();
            string result = channel.Echo(TestString);
            Assert.Equal(TestString, result);
        }
    }

    [SkipOnGeneratedOperationInvokerFact]
    public void ClassBasedServiceContract_WithAlwaysUseAuthorizationPolicySupport_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<ClassBasedUnauthenticatedUserStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding binding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IClassBasedServiceClient>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            IClassBasedServiceClient channel = factory.CreateChannel();
            Assert.Throws<MessageSecurityException>(() => channel.Echo(TestString));
        }
    }

    [SkipOnGeneratedOperationInvokerFact]
    public void ClassBasedServiceContract_WithAlwaysUseAuthorizationPolicySupport_AuthenticatedUser_MissingScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<ClassBasedAuthenticatedUserMissingScopeValuesStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding binding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IClassBasedServiceClient>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            IClassBasedServiceClient channel = factory.CreateChannel();
            Assert.Throws<SecurityAccessDeniedException>(() => channel.Echo(TestString));
        }
    }

    // Service with contract defined on the class itself (not on a separate interface)
    // This tests the fix for BuildAuthorizeData when DeclaringType is not an interface
    [ServiceContract]
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    internal class ClassBasedServiceWithAuthorization
    {
        [OperationContract]
        [Authorize(Policy = AuthorizationUtils.Policies.Read)]
        public string Echo(string text) => text;
    }

    // Client-side interface that matches the service contract
    [System.ServiceModel.ServiceContract(Name = "ClassBasedServiceWithAuthorization", Namespace = "http://tempuri.org/")]
    public interface IClassBasedServiceClient
    {
        [System.ServiceModel.OperationContract]
        string Echo(string text);
    }

    private class ClassBasedAuthZStartup
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
                options.AddPolicy(AuthorizationUtils.Policies.Read,
                    policy => policy.RequireAuthenticatedUser()
                        .RequireClaim("scope", AuthorizationUtils.DefinedScopeValues.Read));
            });
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<ClassBasedServiceWithAuthorization>();
                builder.AddServiceEndpoint<ClassBasedServiceWithAuthorization, ClassBasedServiceWithAuthorization>(ConfigureBinding(), "/service.svc");
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

    private class ClassBasedAuthenticatedUserWithScopeStartup : ClassBasedAuthZStartup
    {
        public ClassBasedAuthenticatedUserWithScopeStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }
    }

    private class ClassBasedUnauthenticatedUserStartup : ClassBasedAuthZStartup
    {
        public ClassBasedUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class ClassBasedAuthenticatedUserMissingScopeValuesStartup : ClassBasedAuthZStartup
    {
        public ClassBasedAuthenticatedUserMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }
    }
}
