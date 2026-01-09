// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Security;
using CoreWCF.Http.Tests.Authorization.Utils;
using CoreWCF.Http.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization;

public class MultiplePoliciesTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public MultiplePoliciesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void MultiplePolicies_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<MultiplePoliciesOnOperationContractWithRequiredScopeValuesStartup>(_output).Build();
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
    public void MultiplePolicies_UnauthenticatedUser_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<MultiplePoliciesOnOperationContractWithUnauthenticatedUserStartup>(_output).Build();
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

    [Theory]
    [InlineData(typeof(MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingWriteScopeValueStartup))]
    [InlineData(typeof(MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingReadScopeValueStartup))]
    public void MultiplePolicies_AuthenticatedUser_ButMissingScopeValues_Test(Type startupType)
    {
        IHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
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



    private class MultiplePoliciesOnOperationContractWithRequiredScopeValuesStartup : AuthZStartup<MultiplePoliciesOnMethodSecuredService>
    {
        public MultiplePoliciesOnOperationContractWithRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Write);
        }
    }

    private class MultiplePoliciesOnOperationContractWithUnauthenticatedUserStartup : AuthZStartup<MultiplePoliciesOnMethodSecuredService>
    {
        public MultiplePoliciesOnOperationContractWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingWriteScopeValueStartup : AuthZStartup<MultiplePoliciesOnMethodSecuredService>
    {
        public MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingWriteScopeValueStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }
    }

    private class MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingReadScopeValueStartup : AuthZStartup<MultiplePoliciesOnMethodSecuredService>
    {
        public MultiplePoliciesOnOperationContractWithAuthenticatedUserButMissingReadScopeValueStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Write);
        }
    }
}
