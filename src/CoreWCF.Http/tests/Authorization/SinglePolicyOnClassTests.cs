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

public class SinglePolicyOnClassTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public SinglePolicyOnClassTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AuthorizeDataOnClass_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<SinglePolicyOnClassWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
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
    public void AuthorizeDataOnClass_UnauthenticatedUser_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<SinglePolicyOnClassWithUnauthenticatedUserStartup>(_output).Build();
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
    public void AuthorizeDataOnClass_AuthenticatedUser_MissingScopeValues_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<SinglePolicyOnClassWithAuthenticatedUserButMissingScopeValuesStartup>(_output).Build();
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



    private class SinglePolicyOnClassWithAuthenticatedUserAndRequiredScopeValuesStartup : AuthZStartup<SinglePolicyOnClassSecuredService>
    {
        public SinglePolicyOnClassWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }
    }

    private class SinglePolicyOnClassWithUnauthenticatedUserStartup : AuthZStartup<SinglePolicyOnClassSecuredService>
    {
        public SinglePolicyOnClassWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class SinglePolicyOnClassWithAuthenticatedUserButMissingScopeValuesStartup : AuthZStartup<SinglePolicyOnClassSecuredService>
    {
        public SinglePolicyOnClassWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }
    }
}
