﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ServiceModel.Security;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Primitives.Tests.CustomSecurity;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class ServiceAuthBehaviorTest
    {
        private readonly ITestOutputHelper _output;
        public ServiceAuthBehaviorTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicHttpRequestReplyEchoWithServiceBehavior()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

        [Fact]
        public void AccessDeniedForBasicHttpRequestReplyEcho()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                SecurityAccessDeniedException ex = Assert.Throws<SecurityAccessDeniedException>(() => channel.EchoToFail(testString));
                Assert.Equal("Access is denied.", ex.Message);
            }
        }


        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton<ServiceAuthorizationManager, MyTestServiceAuthorizationManager>();
            }

            public void Configure(IApplicationBuilder app)
            {
                ServiceAuthorizationBehavior authBehavior = app.ApplicationServices.GetRequiredService<ServiceAuthorizationBehavior>();
                var authPolicies = new List<IAuthorizationPolicy>
                {
                    new MyTestAuthorizationPolicy()
                };
                var externalAuthPolicies = new ReadOnlyCollection<IAuthorizationPolicy>(authPolicies);
                authBehavior.ExternalAuthorizationPolicies = externalAuthPolicies;
                authBehavior.PrincipalPermissionMode = PrincipalPermissionMode.None;
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }
}

