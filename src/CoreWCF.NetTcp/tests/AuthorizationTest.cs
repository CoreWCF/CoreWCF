// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Data;
using System.ServiceModel.Channels;
using System.ServiceModel.Security;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Security;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    public class AuthorizationTest
    {
        private readonly ITestOutputHelper _output;
        public const string FakeAuthRelativePath = "/nettcp.svc/fake-auth";

        public AuthorizationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void AuthorizationBasedonRolesTest()
        {
            string testString = "a" + PrincipalPermissionMode.Always + "test";
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupForAuthorization>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + FakeAuthRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoForAuthorizarionOneRole(testString);
                    Assert.Equal(testString, result);
                    var sade = Assert.Throws<SecurityAccessDeniedException>(() => channel.EchoForAuthorizarionNoRole(testString));
                    Assert.Contains("Access is denied", sade.Message);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }
    }

    public class StartupForAuthorization
    {
        public const string FakeAuthRelativePath = "/nettcp.svc/fake-auth";
        public const string NoSecurityRelativePath = "/nettcp.svc/security-none";
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<Services.TestService>();
                builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new CoreWCF.NetTcpBinding(SecurityMode.None), FakeAuthRelativePath);
                builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new CoreWCF.NetTcpBinding(SecurityMode.None), NoSecurityRelativePath);
                Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                builder.ConfigureServiceHostBase<Services.TestService>(serviceHost);
            });
        }
        public void ChangeHostBehavior(ServiceHostBase host)
        {
            var srvCredentials = new CoreWCF.Description.ServiceCredentials();
            LdapSettings _ldapSettings = new LdapSettings("ldapserver.test.local", "test.local", "your_own_top_org");
            srvCredentials.WindowsAuthentication.LdapSetting = _ldapSettings;
            host.Description.Behaviors.Add(srvCredentials);
            var customPolicy = new CustomAuthorizationPolicy(new Claim(ClaimTypes.Role, "CoreWCFGroupAdmin", Rights.Identity));
            host.Authorization.ExternalAuthorizationPolicies = new List<IAuthorizationPolicy> { customPolicy }.AsReadOnly();
        }
    }
}
