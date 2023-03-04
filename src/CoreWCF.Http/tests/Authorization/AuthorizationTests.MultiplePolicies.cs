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
    public async Task MultiplePolicies_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<MultiplePoliciesOnOperationContractWithRequiredScopeValuesStartup>(_output).Build();
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
    public async Task MultiplePolicies_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<MultiplePoliciesOnOperationContractWithUnauthenticatedUserStartup>(_output).Build();
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

    [Theory]
    [InlineData(typeof(MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingWriteScopeValueStartup))]
    [InlineData(typeof(MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingReadScopeValueStartup))]
    public async Task MultiplePolicies_AuthenticatedUser_ButMissingScopeValues_Test(Type startupType)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
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
    private class MultiplePoliciesOnOperationContractSecuredService : ISecuredService
    {
        [Authorize(Policy = Policies.Read)]
        [Authorize(Policy = Policies.Write)]
        public string Echo(string text) => text;
    }

    private class MultiplePoliciesOnOperationContractWithRequiredScopeValuesStartup : Startup<MultiplePoliciesOnOperationContractSecuredService>
    {
        public MultiplePoliciesOnOperationContractWithRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(DefinedScopeValues.Read);
            ScopeClaimValues.Add(DefinedScopeValues.Write);
        }
    }

    private class MultiplePoliciesOnOperationContractWithUnauthenticatedUserStartup : Startup<MultiplePoliciesOnOperationContractSecuredService>
    {
        public MultiplePoliciesOnOperationContractWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingWriteScopeValueStartup : Startup<MultiplePoliciesOnOperationContractSecuredService>
    {
        public MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingWriteScopeValueStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(DefinedScopeValues.Read);
        }
    }

    private class MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingReadScopeValueStartup : Startup<MultiplePoliciesOnOperationContractSecuredService>
    {
        public MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingReadScopeValueStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(DefinedScopeValues.Write);
        }
    }
}
