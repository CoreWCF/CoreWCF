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

public class DefaultPolicyTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public DefaultPolicyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DefaultPolicy_AuthenticatedUser_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<DefaultPolicyWithAuthenticatedUserAndRequiredClaimValuesStartup>(_output).Build();
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
    public void DefaultPolicy_UnauthenticatedUser_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<DefaultPolicyWithUnauthenticatedUserStartup>(_output).Build();
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
    public void DefaultPolicy_AuthenticatedUser_MissingScopeValues_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<DefaultPolicyWithAuthenticatedUserButMissingScopeClaimValuesStartup>(_output).Build();
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

    private class DefaultPolicyWithAuthenticatedUserAndRequiredClaimValuesStartup : AuthZStartup<DefaultPolicySecuredService>
    {
        public DefaultPolicyWithAuthenticatedUserAndRequiredClaimValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }
    }

    private class DefaultPolicyWithAuthenticatedUserButMissingScopeClaimValuesStartup : AuthZStartup<DefaultPolicySecuredService>
    {
        public DefaultPolicyWithAuthenticatedUserButMissingScopeClaimValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Write);
        }
    }

    private class DefaultPolicyWithUnauthenticatedUserStartup : AuthZStartup<DefaultPolicySecuredService>
    {
        public DefaultPolicyWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }
}
