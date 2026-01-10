// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Security;
using System.Threading.Tasks;
using CoreWCF.Http.Tests.Authorization.Utils;
using CoreWCF.Http.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization;

public class ScopedAuthorizationHandlerTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public ScopedAuthorizationHandlerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SinglePolicy_AuthenticatedUser_HavingRequiredScopeValues_ScopedAuthorizationHandler_Test()
    {
        IWebHost host = ServiceHelper
            .CreateDefaultWebHostBuilder<
                SinglePolicyOnOperationContractWithAuthenticatedUserAndRequiredScopeValuesAndScopedAuthorizationHandlerStartup>(
                _output)
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
#if !NETFRAMEWORK
                options.ValidateOnBuild = true;
#endif
            })
            .Build();
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
    public void SinglePolicy_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper
            .CreateDefaultWebHostBuilder<SinglePolicyOnMethodWithUnauthenticatedUserAndScopedAuthorizationHandlerStartup>(_output)
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
#if !NETFRAMEWORK
                options.ValidateOnBuild = true;
#endif
            })
            .Build();
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
    public void SinglePolicy_AuthenticatedUser_MissingScopeValues_Test()
    {
        IWebHost host = ServiceHelper
            .CreateDefaultWebHostBuilder<
                SinglePolicyOnMethodWithAuthenticatedUserButMissingScopeValuesAndScopedAuthorizationHandlerStartup>(_output)
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
#if !NETFRAMEWORK
                options.ValidateOnBuild = true;
#endif
            })
            .Build();
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

    private class SinglePolicyOnOperationContractWithAuthenticatedUserAndRequiredScopeValuesAndScopedAuthorizationHandlerStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public SinglePolicyOnOperationContractWithAuthenticatedUserAndRequiredScopeValuesAndScopedAuthorizationHandlerStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }

        protected override void OnPostConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IAuthorizationHandler, NoOpAuthorizationHandler>();
        }
    }

    private class SinglePolicyOnMethodWithUnauthenticatedUserAndScopedAuthorizationHandlerStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public SinglePolicyOnMethodWithUnauthenticatedUserAndScopedAuthorizationHandlerStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }

        protected override void OnPostConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IAuthorizationHandler, NoOpAuthorizationHandler>();
        }
    }

    private class SinglePolicyOnMethodWithAuthenticatedUserButMissingScopeValuesAndScopedAuthorizationHandlerStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public SinglePolicyOnMethodWithAuthenticatedUserButMissingScopeValuesAndScopedAuthorizationHandlerStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }

        protected override void OnPostConfigureServices(IServiceCollection services)
        {
            services.AddScoped<IAuthorizationHandler, NoOpAuthorizationHandler>();
        }
    }

    private class NoOpRequirement : IAuthorizationRequirement
    {

    }

    private class NoOpAuthorizationHandler : AuthorizationHandler<NoOpRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
            NoOpRequirement requirement) => Task.CompletedTask;
    }
}
