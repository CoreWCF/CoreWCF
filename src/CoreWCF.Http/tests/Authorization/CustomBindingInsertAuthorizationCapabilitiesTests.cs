// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ServiceModel.Security;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Http.Tests.Authorization.Utils;
using CoreWCF.Http.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization;

public class CustomBindingInsertAuthorizationCapabilitiesTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public CustomBindingInsertAuthorizationCapabilitiesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void CustomBinding_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<CustomBindingWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
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
    public void CustomBinding_UnauthenticatedUser_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<CustomBindingWithUnauthenticatedUserStartup>(_output).Build();
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
    public void CustomBinding_AuthenticatedUser_MissingScopeValues_Test()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<CustomBindingWithAuthenticatedUserButMissingScopeValuesStartup>(_output).Build();
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

    private class SupportsAuthorizationBindingElement : BindingElement
    {
        public override BindingElement Clone() => new SupportsAuthorizationBindingElement();

        public override T GetProperty<T>(BindingContext context)
        {
            if (typeof(T) == typeof(IAuthorizationCapabilities))
            {
                var binding = context.BindingParameters.Find<Binding>();
                if (binding is CustomBinding customBinding)
                {
                    context.BindingParameters.Remove(customBinding);
                    return (T)(object)new CustomBindingAuthorizationCapabilities();
                }

                return null;
            }

            return context.GetInnerProperty<T>();
        }
    }

    private class CustomBindingAuthorizationCapabilities : IAuthorizationCapabilities
    {
        public bool SupportsAuthorizationData => true;
    }

    private class CustomBindingWithAuthenticatedUserAndRequiredScopeValuesStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public CustomBindingWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }

        protected override string AuthenticationScheme => AuthenticationSchemes.Basic.ToString();

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            CustomBinding customBinding = new CustomBinding(basicHttpBinding);
            customBinding.Elements.Insert(0, new SupportsAuthorizationBindingElement());
            return customBinding;
        }
    }

    private class CustomBindingWithUnauthenticatedUserStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public CustomBindingWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }

        protected override string AuthenticationScheme => AuthenticationSchemes.Basic.ToString();

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            CustomBinding customBinding = new CustomBinding(basicHttpBinding);
            customBinding.Elements.Insert(0, new SupportsAuthorizationBindingElement());
            return customBinding;
        }
    }

    private class CustomBindingWithAuthenticatedUserButMissingScopeValuesStartup : AuthZStartup<SinglePolicyOnMethodSecuredService>
    {
        public CustomBindingWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }

        protected override string AuthenticationScheme => AuthenticationSchemes.Basic.ToString();

        protected override Binding ConfigureBinding()
        {
            BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
            basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
            CustomBinding customBinding = new CustomBinding(basicHttpBinding);
            customBinding.Elements.Insert(0, new SupportsAuthorizationBindingElement());
            return customBinding;
        }
    }
}
