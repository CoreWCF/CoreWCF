// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Security;
using CoreWCF.Http.Tests.Authorization.Utils;
using Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization;

public class FallbackPolicyTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public FallbackPolicyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void FallbackPolicy_AuthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<FallbackPolicyWithAuthenticatedUserStartup>(_output).Build();
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
    public void FallbackPolicy_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<FallbackPolicyWithUnauthenticatedUserStartup>(_output).Build();
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

    private class FallbackPolicyWithAuthenticatedUserStartup : AuthZStartup<FallbackPolicySecuredService>
    {
        public FallbackPolicyWithAuthenticatedUserStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }
    }

    private class FallbackPolicyWithUnauthenticatedUserStartup : AuthZStartup<FallbackPolicySecuredService>
    {
        public FallbackPolicyWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }
}
