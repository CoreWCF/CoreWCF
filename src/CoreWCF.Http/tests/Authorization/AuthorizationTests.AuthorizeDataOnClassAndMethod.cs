// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Security;
using System.Threading.Tasks;
using Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace CoreWCF.Http.Tests.Authorization;

public partial class AuthorizationTests
{
    [Fact]
    public async Task AuthorizeDataOnClassAndMethod_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<AuthorizeDataOnClassAndMethodWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
        using (host)
        {
            await host.StartAsync();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
            ISecuredService channel = factory.CreateChannel();
            string result = channel.Echo(TestString);
            Assert.Equal(TestString, result);
        }
    }

    [Fact]
    public async Task AuthorizeDataOnClassAndMethod_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<AuthorizeDataOnClassAndMethodWithUnauthenticatedUserStartup>(_output).Build();
        using (host)
        {
            await host.StartAsync();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<MessageSecurityException>(() => channel.Echo(TestString));
        }
    }

    [Theory]
    [InlineData(typeof(AuthorizeDataOnClassAndMethodWithAuthenticatedUserButMissingReadScopeValueStartup))]
    [InlineData(typeof(AuthorizeDataOnClassAndMethodWithAuthenticatedUserButMissingWriteScopeValueStartup))]
    public async Task AuthorizeDataOnClassAndMethod_AuthenticatedUser_MissingScopeValues_Test(Type startupType)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
        using (host)
        {
            await host.StartAsync();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<SecurityAccessDeniedException>(() => channel.Echo(TestString));
        }
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    [Authorize(Policy = Policies.Read)]
    private class AuthorizeDataOnClassAndMethodSecuredService : ISecuredService
    {
        [Authorize(Policy = Policies.Write)]
        public string Echo(string text) => text;
    }

    private class AuthorizeDataOnClassAndMethodWithAuthenticatedUserAndRequiredScopeValuesStartup : Startup<AuthorizeDataOnClassAndMethodSecuredService>
    {
        public AuthorizeDataOnClassAndMethodWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(DefinedScopeValues.Read);
            ScopeClaimValues.Add(DefinedScopeValues.Write);
        }
    }

    private class AuthorizeDataOnClassAndMethodWithUnauthenticatedUserStartup : Startup<AuthorizeDataOnClassAndMethodSecuredService>
    {
        public AuthorizeDataOnClassAndMethodWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class AuthorizeDataOnClassAndMethodWithAuthenticatedUserButMissingReadScopeValueStartup : Startup<AuthorizeDataOnClassAndMethodSecuredService>
    {
        public AuthorizeDataOnClassAndMethodWithAuthenticatedUserButMissingReadScopeValueStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(DefinedScopeValues.Write);
        }
    }

    private class AuthorizeDataOnClassAndMethodWithAuthenticatedUserButMissingWriteScopeValueStartup : Startup<AuthorizeDataOnClassAndMethodSecuredService>
    {
        public AuthorizeDataOnClassAndMethodWithAuthenticatedUserButMissingWriteScopeValueStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(DefinedScopeValues.Read);
        }
    }
}
