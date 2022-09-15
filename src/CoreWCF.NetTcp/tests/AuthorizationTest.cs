// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ServiceModel.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Primitives.Tests.CustomSecurity;
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

        public AuthorizationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact(Skip ="Skipped in pipeline run")]
        public void AuthorizationBasedonRolesTest()
        {
            string testString = "a" + PrincipalPermissionMode.Always.ToString() + "test";
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupForAuthorization>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.Transport);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.WindowsAuthRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoForAuthorizarionOneRole(testString);
                    Assert.Equal(testString, result);
                    try
                    {
                        channel.EchoForAuthorizarionNoRole(testString);
                    }catch(Exception ex)
                    {
                        Assert.Contains("Access is denied", ex.Message);
                    }
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
        public const string WindowsAuthRelativePath = "/nettcp.svc/windows-auth";
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
                builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new CoreWCF.NetTcpBinding(), WindowsAuthRelativePath);
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
        }
    }
}
