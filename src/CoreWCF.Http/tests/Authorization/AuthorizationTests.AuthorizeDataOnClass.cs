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
    public async Task AuthorizeDataOnClass_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<AuthorizeDataOnClassWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
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
    public async Task AuthorizeDataOnClass_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<AuthorizeDataOnClassWithUnauthenticatedUserStartup>(_output).Build();
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
    public async Task AuthorizeDataOnClass_AuthenticatedUser_MissingScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<AuthorizeDataOnClassWithAuthenticatedUserButMissingScopeValuesStartup>(_output).Build();
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
    [Authorize(Policy = Policies.Read)]
    private class AuthorizeDataOnClassSecuredService : ISecuredService
    {
        public string Echo(string text) => text;
    }

    private class AuthorizeDataOnClassWithAuthenticatedUserAndRequiredScopeValuesStartup : Startup<AuthorizeDataOnClassSecuredService>
    {
        public AuthorizeDataOnClassWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(DefinedScopeValues.Read);
        }
    }

    private class AuthorizeDataOnClassWithUnauthenticatedUserStartup : Startup<AuthorizeDataOnClassSecuredService>
    {
        public AuthorizeDataOnClassWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class AuthorizeDataOnClassWithAuthenticatedUserButMissingScopeValuesStartup : Startup<AuthorizeDataOnClassSecuredService>
    {
        public AuthorizeDataOnClassWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }
    }
}
