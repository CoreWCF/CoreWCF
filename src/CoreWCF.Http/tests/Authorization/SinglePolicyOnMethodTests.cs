// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Security;
using CoreWCF.Http.Tests.Authorization.Utils;
using CoreWCF.Http.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using ServiceContract;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization;

public class SinglePolicyOnMethodTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public SinglePolicyOnMethodTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SinglePolicy_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper
            .CreateWebHostBuilder<
                SinglePolicyOnMethodWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
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
            .CreateWebHostBuilder<SinglePolicyOnMethodWithUnauthenticatedUserStartup>(_output).Build();
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
            .CreateWebHostBuilder<
                SinglePolicyOnMethodWithAuthenticatedUserButMissingScopeValuesStartup>(_output).Build();
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

    private class SinglePolicyOnMethodWithAuthenticatedUserAndRequiredScopeValuesStartup : AuthZStartup<Services.SinglePolicyOnMethodSecuredService>
    {
        public SinglePolicyOnMethodWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }
    }

    private class SinglePolicyOnMethodWithUnauthenticatedUserStartup : AuthZStartup<Services.SinglePolicyOnMethodSecuredService>
    {
        public SinglePolicyOnMethodWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class SinglePolicyOnMethodWithAuthenticatedUserButMissingScopeValuesStartup : AuthZStartup<Services.SinglePolicyOnMethodSecuredService>
    {
        public SinglePolicyOnMethodWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }
    }
}

