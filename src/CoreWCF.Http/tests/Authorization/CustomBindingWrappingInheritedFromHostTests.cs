// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Security;
using CoreWCF.Channels;
using CoreWCF.Http.Tests.Authorization.Utils;
using CoreWCF.Http.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization;

public class CustomBindingWrappingInheritedFromHostTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public CustomBindingWrappingInheritedFromHostTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CustomBinding_WrappingInheritedFromHost_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<CustomBindingWrappingInheritedFromHostWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding binding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            ISecuredService channel = factory.CreateChannel();
            string result = channel.Echo(TestString);
            Assert.Equal(TestString, result);
        }
    }

    [Fact]
    public void CustomBinding_WrappingInheritedFromHost_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<CustomBindingWrappingInheritedFromHostWithUnauthenticatedUserStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding binding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<MessageSecurityException>(() => channel.Echo(TestString));
        }
    }

    [Fact]
    public void CustomBinding_WrappingInheritedFromHost_AuthenticatedUser_MissingScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<CustomBindingWrappingInheritedFromHostWithAuthenticatedUserButMissingScopeValuesStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding binding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<SecurityAccessDeniedException>(() => channel.Echo(TestString));
        }
    }

    private class CustomBindingWrappingInheritedFromHostWithAuthenticatedUserAndRequiredScopeValuesStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public CustomBindingWrappingInheritedFromHostWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.InheritedFromHost;
            CustomBinding customBinding = new CustomBinding(basicHttpBinding);
            return customBinding;
        }
    }

    private class CustomBindingWrappingInheritedFromHostWithUnauthenticatedUserStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public CustomBindingWrappingInheritedFromHostWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.InheritedFromHost;
            CustomBinding customBinding = new CustomBinding(basicHttpBinding);
            return customBinding;
        }
    }

    private class CustomBindingWrappingInheritedFromHostWithAuthenticatedUserButMissingScopeValuesStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public CustomBindingWrappingInheritedFromHostWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.InheritedFromHost;
            CustomBinding customBinding = new CustomBinding(basicHttpBinding);
            return customBinding;
        }
    }
}
