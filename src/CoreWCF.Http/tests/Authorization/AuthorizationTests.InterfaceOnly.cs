// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Security;
using System.Threading.Tasks;
using Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CoreWCF.Http.Tests.Authorization;

public partial class AuthorizationTests
{
    [Fact]
    public async Task InterfaceOnly_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<InterfaceOnlyWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
        using (host)
        {
            await host.StartAsync();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
            ISecuredService channel = factory.CreateChannel();
            string result = channel.Echo(TestString);
            Assert.Equal(TestString, result);
        }
    }

    [Fact]
    public async Task InterfaceOnly_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<InterfaceOnlyWithUnauthenticatedUserStartup>(_output).Build();
        using (host)
        {
            await host.StartAsync();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<MessageSecurityException>(() => channel.Echo(TestString));
        }
    }

    [Fact]
    public async Task InterfaceOnly_AuthenticatedUser_MissingScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<InterfaceOnlyWithAuthenticatedUserButMissingScopeValuesStartup>(_output).Build();
        using (host)
        {
            await host.StartAsync();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<SecurityAccessDeniedException>(() => channel.Echo(TestString));
        }
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    private class InterfaceOnlySecuredService : ISecuredService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public InterfaceOnlySecuredService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            Assert.NotNull(_httpContextAccessor.HttpContext);
        }

        [Authorize(Policy = Policies.Read)]
        public string Echo(string text)
        {
            Assert.NotNull(_httpContextAccessor.HttpContext);
            return text;
        }
    }

    private class InterfaceOnlyWithAuthenticatedUserAndRequiredScopeValuesStartup : Startup<InterfaceOnlySecuredService>
    {
        public InterfaceOnlyWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(DefinedScopeValues.Read);
        }
    }

    private class InterfaceOnlyWithUnauthenticatedUserStartup : Startup<InterfaceOnlySecuredService>
    {
        public InterfaceOnlyWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class InterfaceOnlyWithAuthenticatedUserButMissingScopeValuesStartup : Startup<InterfaceOnlySecuredService>
    {
        public InterfaceOnlyWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }
    }
}
