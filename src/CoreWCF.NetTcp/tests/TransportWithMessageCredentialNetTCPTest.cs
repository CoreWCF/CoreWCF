// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Runtime.ConstrainedExecution;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Channels;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security;
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
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                    new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.WindowsAuthRelativePath));
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
                        }); ;
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

        private static Random random = new Random();
        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
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
                srvCredentials.ServiceCertificate.Certificate=  ServiceHelper.GetServiceCertificate();
                srvCredentials.UserNameAuthentication.UserNamePasswordValidationMode
              = CoreWCF.Security.UserNamePasswordValidationMode.Custom;
                srvCredentials.UserNameAuthentication.CustomUserNamePasswordValidator
                    = new CustomTestValidator();
                host.Description.Behaviors.Add(srvCredentials);
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                CoreWCF.NetTcpBinding serverBinding = new CoreWCF.NetTcpBinding(SecurityMode.TransportWithMessageCredential);
                serverBinding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(serverBinding, WindowsAuthRelativePath);
                    Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                    builder.ConfigureServiceHostBase<Services.TestService>(serviceHost);
                });
            }

            internal class CustomTestValidator : UserNamePasswordValidator
            {
                public override void Validate(string userName, string password)
                {
                    if (string.Compare(userName, "testuser@corewcf", StringComparison.OrdinalIgnoreCase) == 0 && password.Length >0)
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
