﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Linq;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Selectors;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    public class TransportWithMessageCredentialNetTCPTest
    {
        private readonly ITestOutputHelper _output;

        public TransportWithMessageCredentialNetTCPTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(false, "testuser@corewcf")]
        [InlineData(true, "randomuser@corewcf")]
        private void BasicUserNameAuth(bool isError, string userName)
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartUpPermissionBaseForTC>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                binding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                UriBuilder uriBuilder = new UriBuilder(host.GetNetTcpAddressInUse() + Startup.WindowsAuthRelativePath);
                uriBuilder.Host = "localhost"; // Replace 127.0.0.1 with localhost so Identity has correct value
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                    new System.ServiceModel.EndpointAddress(uriBuilder.ToString()));
                System.ServiceModel.Description.ClientCredentials clientCredentials = (System.ServiceModel.Description.ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(System.ServiceModel.Description.ClientCredentials)];
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                clientCredentials.UserName.UserName = userName;
                clientCredentials.UserName.Password = RandomString(10);
                var channel = factory.CreateChannel();
                try
                {
                    if (isError)
                    {
                        Assert.ThrowsAny<System.ServiceModel.CommunicationException>(() =>
                        {
                            ((IChannel)channel).Open();
                        });

                        ((IChannel)channel).Abort();
                    }
                    else
                    {
                        ((IChannel)channel).Open();
                        string result = channel.EchoString(testString);
                        Assert.Equal(testString, result);
                        ((IChannel)channel).Close();
                        factory.Close();
                    }
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact, Description("Demuxer-failure-nettcp")]
        [UseCulture("en-US")]
        public async Task NetTCPRequestReplyWithTransportMessageEchoStringDemuxFailure()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartUpPermissionBaseForTCDemuxFailure>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                binding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                UriBuilder uriBuilder = new UriBuilder(host.GetNetTcpAddressInUse() + Startup.WindowsAuthRelativePath);
                uriBuilder.Host = "localhost"; // Replace 127.0.0.1 with localhost so Identity has correct value
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                    new System.ServiceModel.EndpointAddress(uriBuilder.ToString()));
                System.ServiceModel.Description.ClientCredentials clientCredentials = (System.ServiceModel.Description.ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(System.ServiceModel.Description.ClientCredentials)];
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                clientCredentials.UserName.UserName = "testuser@corewcf";
                clientCredentials.UserName.Password = RandomString(10);
                var channel = factory.CreateChannel();
                try
                {
                    ((IChannel)channel).Open();
                    await Task.Delay(6000);
                    string result = channel.EchoString(testString);
                }
                catch (Exception ex)
                {
                    Assert.IsAssignableFrom<System.ServiceModel.FaultException>(ex.InnerException);
                    Assert.Contains("expired security context token", ex.InnerException.Message);
                }
            }
        }

        private static Random s_random = new Random();
        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[s_random.Next(s.Length)]).ToArray());
        }

        public class StartUpPermissionBaseForTCDemuxFailure : StartUpPermissionBaseForTC
        {
            public override CoreWCF.Channels.Binding ChangeBinding(NetTcpBinding binding)
            {
                CoreWCF.Channels.CustomBinding customBinding = new CoreWCF.Channels.CustomBinding(binding);
                CoreWCF.Channels.SecurityBindingElement security = customBinding.Elements.Find<CoreWCF.Channels.SecurityBindingElement>();
                security.LocalServiceSettings.InactivityTimeout = TimeSpan.FromSeconds(3);
                return customBinding;
            }
        }

        public class StartUpPermissionBaseForTC
        {
            public const string WindowsAuthRelativePath = "/nettcp.svc/windows-auth";
            public const string NoSecurityRelativePath = "/nettcp.svc/security-none";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = new CoreWCF.Description.ServiceCredentials();
                srvCredentials.ServiceCertificate.Certificate = ServiceHelper.GetServiceCertificate();
                srvCredentials.UserNameAuthentication.UserNamePasswordValidationMode =
                        CoreWCF.Security.UserNamePasswordValidationMode.Custom;
                srvCredentials.UserNameAuthentication.CustomUserNamePasswordValidator =
                        new CustomTestValidator();
                host.Description.Behaviors.Add(srvCredentials);
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                CoreWCF.NetTcpBinding serverBinding = new CoreWCF.NetTcpBinding(SecurityMode.TransportWithMessageCredential);
                serverBinding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(ChangeBinding(serverBinding), WindowsAuthRelativePath);
                    Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                    builder.ConfigureServiceHostBase<Services.TestService>(serviceHost);
                });
            }

            public virtual CoreWCF.Channels.Binding ChangeBinding(NetTcpBinding netTCPBinding) => netTCPBinding;

            internal class CustomTestValidator : UserNamePasswordValidator
            {
                public override void Validate(string userName, string password)
                {
                    if (string.Compare(userName, "testuser@corewcf", StringComparison.OrdinalIgnoreCase) == 0 && password.Length > 0)
                    {
                        //Write custom logic
                        return;
                    }
                    else
                    {
                        throw new Exception("Permission Denied");
                    }
                }
            }
        }
    }
}
