// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Security;
using CoreWCF.Http.Tests.Authorization.Utils;
using CoreWCF.Http.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization;

public class MultiplePoliciesOnClassAndMethodTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public MultiplePoliciesOnClassAndMethodTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AuthorizeDataOnClassAndMethod_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<MultiplePoliciesOnClassAndMethodWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
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
    public void AuthorizeDataOnClassAndMethod_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<MultiplePoliciesOnClassAndMethodWithUnauthenticatedUserStartup>(_output).Build();
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
    [InlineData(typeof(MultiplePoliciesOnClassAndMethodWithAuthenticatedUserButMissingReadScopeValueStartup))]
    [InlineData(typeof(MultiplePoliciesOnClassAndMethodWithAuthenticatedUserButMissingWriteScopeValueStartup))]
    public void AuthorizeDataOnClassAndMethod_AuthenticatedUser_MissingScopeValues_Test(Type startupType)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
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

    private class MultiplePoliciesOnClassAndMethodWithAuthenticatedUserAndRequiredScopeValuesStartup : AuthZStartup<MultiplePoliciesOnClassAndMethodSecuredService>
    {
        public MultiplePoliciesOnClassAndMethodWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Write);
        }
    }

    private class MultiplePoliciesOnClassAndMethodWithUnauthenticatedUserStartup : AuthZStartup<MultiplePoliciesOnClassAndMethodSecuredService>
    {
        public MultiplePoliciesOnClassAndMethodWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class MultiplePoliciesOnClassAndMethodWithAuthenticatedUserButMissingReadScopeValueStartup : AuthZStartup<MultiplePoliciesOnClassAndMethodSecuredService>
    {
        public MultiplePoliciesOnClassAndMethodWithAuthenticatedUserButMissingReadScopeValueStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Write);
        }
    }

    private class MultiplePoliciesOnClassAndMethodWithAuthenticatedUserButMissingWriteScopeValueStartup : AuthZStartup<MultiplePoliciesOnClassAndMethodSecuredService>
    {
        public MultiplePoliciesOnClassAndMethodWithAuthenticatedUserButMissingWriteScopeValueStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }
    }
}
