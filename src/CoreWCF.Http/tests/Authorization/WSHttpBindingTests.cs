﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ServiceModel.Security;
using CoreWCF.Configuration;
using CoreWCF.Http.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization;

public class WSHttpBindingTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public WSHttpBindingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void WSHttpBinding_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpBindingWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.Transport);
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(wsHttpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/BasicWcfService/wshttp.svc")));
            factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new X509ServiceCertificateAuthentication
            {
                CertificateValidationMode = X509CertificateValidationMode.None
            };
            ISecuredService channel = factory.CreateChannel();
            string result = channel.Echo(TestString);
            Assert.Equal(TestString, result);
        }
    }

    [Fact]
    public void WSHttpBinding_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpBindingWithUnauthenticatedUserStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.Transport);
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(wsHttpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/BasicWcfService/wshttp.svc")));
            factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new X509ServiceCertificateAuthentication
            {
                CertificateValidationMode = X509CertificateValidationMode.None
            };
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<MessageSecurityException>(() => channel.Echo(TestString));
        }
    }

    [Fact]
    public void WSHttpBinding_AuthenticatedUser_MissingScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpBindingWithAuthenticatedUserButMissingScopeValuesStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.Transport);
            var factory = new System.ServiceModel.ChannelFactory<ISecuredService>(wsHttpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/BasicWcfService/wshttp.svc")));
            factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new X509ServiceCertificateAuthentication
            {
                CertificateValidationMode = X509CertificateValidationMode.None
            };
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<SecurityAccessDeniedException>(() => channel.Echo(TestString));
        }
    }

    private abstract class WSHttpBindingStartup<TSecuredService> where TSecuredService : class, ISecuredService
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

            services.AddAuthentication(FakeJwtBearerAuthenticationHandler.AuthenticationScheme)
                .AddScheme<FakeJwtBearerAuthenticationHandlerOptions, FakeJwtBearerAuthenticationHandler>(FakeJwtBearerAuthenticationHandler.AuthenticationScheme,
                    options =>
                    {
                        options.IsAuthenticated = IsAuthenticated;
                        options.ScopeClaimValues = ScopeClaimValues;
                    });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationUtils.Policies.Write,
                    policy => policy.RequireAuthenticatedUser().RequireClaim("scope", AuthorizationUtils.DefinedScopeValues.Write));
                options.AddPolicy(AuthorizationUtils.Policies.Read,
                    policy => policy.RequireAuthenticatedUser().RequireClaim("scope", AuthorizationUtils.DefinedScopeValues.Read));
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
                builder.AddServiceEndpoint<TSecuredService, ISecuredService>(
                    new WSHttpBinding()
                    {
                        Security = new WSHTTPSecurity()
                        {
                            Mode = SecurityMode.Transport,
                            Transport = new HttpTransportSecurity
                            {
                                ClientCredentialType = HttpClientCredentialType.InheritedFromHost
                            }
                        }
                    }, "/BasicWcfService/wshttp.svc");
            });
        }
    }

    private class WSHttpBindingWithAuthenticatedUserAndRequiredScopeValuesStartup : WSHttpBindingStartup<SinglePolicyOnMethodSecuredService>
    {
        public WSHttpBindingWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(AuthorizationUtils.DefinedScopeValues.Read);
        }
    }

    private class WSHttpBindingWithUnauthenticatedUserStartup : WSHttpBindingStartup<SinglePolicyOnMethodSecuredService>
    {
        public WSHttpBindingWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class WSHttpBindingWithAuthenticatedUserButMissingScopeValuesStartup : WSHttpBindingStartup<SinglePolicyOnMethodSecuredService>
    {
        public WSHttpBindingWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }
    }
}
