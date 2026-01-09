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

public class AlwaysUseAuthorizationPolicySupportTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public AlwaysUseAuthorizationPolicySupportTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Theory]
    [InlineData(typeof(CustomBindingHttpTransportBindingElementAlwaysUseAuthorizationPolicySupportWithAuthenticatedUserAndRequiredScopeValuesStartup))]
    [InlineData(typeof(BasicHttpBindingHttpTransportSecurityUseAuthorizationPolicySupportWithAuthenticatedUserAndRequiredScopeValuesStartup))]
    public void CustomBindingSupportsAuthorizationData_AuthenticatedUser_HavingRequiredScopeValues_Test(Type startupType)
    {
        IHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
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

    [Theory]
    [InlineData(typeof(CustomBindingHttpTransportBindingElementAlwaysUseAuthorizationPolicySupportWithUnauthenticatedUserStartup))]
    [InlineData(typeof(BasicHttpBindingHttpTransportSecurityUseAuthorizationPolicySupportWithUnauthenticatedUserStartup))]
    public void CustomBindingSupportsAuthorizationData_UnauthenticatedUser_Test(Type startupType)
    {
        IHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
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

    [Theory]
    [InlineData(typeof(CustomBindingHttpTransportBindingElementAlwaysUseAuthorizationPolicySupportWithAuthenticatedUserButMissingScopeValuesStartup))]
    [InlineData(typeof(BasicHttpBindingHttpTransportSecurityUseAuthorizationPolicySupportWithAuthenticatedUserButMissingScopeValuesStartup))]
    public void CustomBindingSupportsAuthorizationData_AuthenticatedUser_MissingScopeValues_Test(Type startupType)
    {
        IHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
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

    private class CustomBindingHttpTransportBindingElementAlwaysUseAuthorizationPolicySupportWithAuthenticatedUserAndRequiredScopeValuesStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public CustomBindingHttpTransportBindingElementAlwaysUseAuthorizationPolicySupportWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            CustomBinding customBinding = new CustomBinding(basicHttpBinding);
            HttpTransportBindingElement transportBindingElement = customBinding.Elements.Find<HttpTransportBindingElement>();
            transportBindingElement.AlwaysUseAuthorizationPolicySupport = true;
            return customBinding;
        }

        protected override string AuthenticationScheme => HttpClientCredentialType.Basic.ToString();
    }

    private class BasicHttpBindingHttpTransportSecurityUseAuthorizationPolicySupportWithAuthenticatedUserAndRequiredScopeValuesStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public BasicHttpBindingHttpTransportSecurityUseAuthorizationPolicySupportWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            basicHttpBinding.Security.Transport.AlwaysUseAuthorizationPolicySupport = true;
            return basicHttpBinding;
        }

        protected override string AuthenticationScheme => HttpClientCredentialType.Basic.ToString();
    }

    private class CustomBindingHttpTransportBindingElementAlwaysUseAuthorizationPolicySupportWithUnauthenticatedUserStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public CustomBindingHttpTransportBindingElementAlwaysUseAuthorizationPolicySupportWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            CustomBinding customBinding = new CustomBinding(basicHttpBinding);
            HttpTransportBindingElement transportBindingElement = customBinding.Elements.Find<HttpTransportBindingElement>();
            transportBindingElement.AlwaysUseAuthorizationPolicySupport = true;
            return customBinding;
        }

        protected override string AuthenticationScheme => HttpClientCredentialType.Basic.ToString();
    }

    private class BasicHttpBindingHttpTransportSecurityUseAuthorizationPolicySupportWithUnauthenticatedUserStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public BasicHttpBindingHttpTransportSecurityUseAuthorizationPolicySupportWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            basicHttpBinding.Security.Transport.AlwaysUseAuthorizationPolicySupport = true;
            return basicHttpBinding;
        }

        protected override string AuthenticationScheme => HttpClientCredentialType.Basic.ToString();
    }

    private class CustomBindingHttpTransportBindingElementAlwaysUseAuthorizationPolicySupportWithAuthenticatedUserButMissingScopeValuesStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public CustomBindingHttpTransportBindingElementAlwaysUseAuthorizationPolicySupportWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            CustomBinding customBinding = new CustomBinding(basicHttpBinding);
            HttpTransportBindingElement transportBindingElement = customBinding.Elements.Find<HttpTransportBindingElement>();
            transportBindingElement.AlwaysUseAuthorizationPolicySupport = true;
            return customBinding;
        }

        protected override string AuthenticationScheme => HttpClientCredentialType.Basic.ToString();
    }

    private class BasicHttpBindingHttpTransportSecurityUseAuthorizationPolicySupportWithAuthenticatedUserButMissingScopeValuesStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public BasicHttpBindingHttpTransportSecurityUseAuthorizationPolicySupportWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            basicHttpBinding.Security.Transport.AlwaysUseAuthorizationPolicySupport = true;
            return basicHttpBinding;
        }

        protected override string AuthenticationScheme => HttpClientCredentialType.Basic.ToString();
    }
}
