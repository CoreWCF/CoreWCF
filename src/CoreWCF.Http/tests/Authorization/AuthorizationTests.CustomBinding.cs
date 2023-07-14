// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.ServiceModel.Security;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.Http.Tests.Authorization;

public partial class AuthorizationTests
{
    [Fact]
    public void CustomBinding_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<CustomBindingWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding binding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/custom.svc")));
            ISecuredService channel = factory.CreateChannel();
            string result = channel.Echo(TestString);
            Assert.Equal(TestString, result);
        }
    }

    [Fact]
    public void CustomBinding_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<CustomBindingWithUnauthenticatedUserStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding binding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/custom.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<MessageSecurityException>(() => channel.Echo(TestString));
        }
    }

    [Fact]
    public void CustomBinding_AuthenticatedUser_MissingScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<CustomBindingWithAuthenticatedUserButMissingScopeValuesStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding binding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/custom.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<SecurityAccessDeniedException>(() => channel.Echo(TestString));
        }
    }

    private abstract class CustomBindingStartup<TSecuredService> where TSecuredService : class, ISecuredService
    {
        public bool IsAuthenticated { get; set; }
        public List<string> ScopeClaimValues { get; set; } = new();

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<FakeJwtBearerAuthenticationHandlerOptions>(options =>
            {
                options.IsAuthenticated = IsAuthenticated;
                options.ScopeClaimValues = ScopeClaimValues;
            });

            services.AddAuthentication("Basic")
                .AddScheme<FakeJwtBearerAuthenticationHandlerOptions, FakeJwtBearerAuthenticationHandler>("Basic",
                    options =>
                    {
                        options.IsAuthenticated = IsAuthenticated;
                        options.ScopeClaimValues = ScopeClaimValues;
                    });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(Policies.Write,
                    policy => policy.RequireAuthenticatedUser().RequireClaim("scope", DefinedScopeValues.Write));
                options.AddPolicy(Policies.Read,
                    policy => policy.RequireAuthenticatedUser().RequireClaim("scope", DefinedScopeValues.Read));
            });
            services.AddServiceModelServices();
            services.AddHttpContextAccessor();
            if (typeof(TSecuredService).IsInterface)
            {
                services.AddTransient<ISecuredService, TSecuredService>();
            }
            else
            {
                services.AddTransient<TSecuredService>();
            }
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<TSecuredService>();
                BasicHttpBinding basicHttpBinding = new BasicHttpBinding(BasicHttpSecurityMode.TransportCredentialOnly);
                basicHttpBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;
                CustomBinding customBinding = new CustomBinding(basicHttpBinding);
                customBinding.Elements.Insert(0, new SupportsAuthorizationBindingElement());
                builder.AddServiceEndpoint<TSecuredService, ISecuredService>(customBinding, "/BasicWcfService/custom.svc");
            });
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


    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    private class CustomBindingSecuredService : ISecuredService
    {
        [Authorize(Policy = Policies.Read)]
        public string Echo(string text) => text;
    }

    private class CustomBindingWithAuthenticatedUserAndRequiredScopeValuesStartup : CustomBindingStartup<CustomBindingSecuredService>
    {
        public CustomBindingWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(DefinedScopeValues.Read);
        }
    }

    private class CustomBindingWithUnauthenticatedUserStartup : CustomBindingStartup<CustomBindingSecuredService>
    {
        public CustomBindingWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class CustomBindingWithAuthenticatedUserButMissingScopeValuesStartup : CustomBindingStartup<CustomBindingSecuredService>
    {
        public CustomBindingWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }
    }
}
