// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
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
        public const string WindowsAuthRelativePath = "/nettcp.svc/windows-auth";

        public TransportWithMessageCredentialNetTCPTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData(false, "testuser@corewcf")]
        [InlineData(true, "randomuser@corewcf")]
        public void BasicUserNameAuth(bool isError, string userName)
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartUpPermissionBaseForTC>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                binding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                UriBuilder uriBuilder = new UriBuilder(host.GetNetTcpAddressInUse() + WindowsAuthRelativePath);
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

        [Fact(Skip = "https://github.com/CoreWCF/CoreWCF/issues/830"), Description("Demuxer-failure-nettcp")]
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
                UriBuilder uriBuilder = new UriBuilder(host.GetNetTcpAddressInUse() + WindowsAuthRelativePath);
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

        [Fact]
        public void SecurityHeaderRoleIsOmmitted()
        {
            // This test verifies that the security header role attribute is ommitted
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartUpPermissionBaseForTC>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                binding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                UriBuilder uriBuilder = new UriBuilder(host.GetNetTcpAddressInUse() + WindowsAuthRelativePath);
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
                    string result;
                    using (var scope = new System.ServiceModel.OperationContextScope((System.ServiceModel.IContextChannel)channel))
                    {
                        result = channel.EchoString(testString);
                        var opContext = System.ServiceModel.OperationContext.Current;
                        int securityHeaderIndex = opContext.IncomingMessageHeaders.FindHeader("Security", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
                        var reader = opContext.IncomingMessageHeaders.GetReaderAtHeader(securityHeaderIndex);
                        Assert.Null(reader.GetAttribute("role", "http://www.w3.org/2003/05/soap-envelope"));
                    }
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact]
        private void BasicNetTcpWithCertificateAsTransport()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupForNetTcpCertificate>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetStreamedModeBinding(System.ServiceModel.SecurityMode.Transport);
                binding.Security.Transport.ClientCredentialType = System.ServiceModel.TcpClientCredentialType.Certificate;
                UriBuilder uriBuilder = new UriBuilder(host.GetNetTcpAddressInUse() + WindowsAuthRelativePath);
                uriBuilder.Host = "localhost";
                binding.Security.Transport.SslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                    new System.ServiceModel.EndpointAddress(uriBuilder.ToString()));

                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };

                ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.ClientCertificate.Certificate = ServiceHelper.GetServiceCertificate();//ensuring some validation such cert present or not covered
                var channel = factory.CreateChannel();
                try
                {
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
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

            public void Configure(IApplicationBuilder app)
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
                public override ValueTask ValidateAsync(string userName, string password)
                {
                    if (string.Compare(userName, "testuser@corewcf", StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return new ValueTask(Task.CompletedTask);
                    }

                    return new ValueTask(Task.FromException(new Exception("Permission Denied")));
                }
            }
        }


        public class StartupForNetTcpCertificate
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    var netTcpBInding = new NetTcpBinding
                    {
                        Security = new NetTcpSecurity
                        {
                            Mode = SecurityMode.Transport,
                            Transport = new TcpTransportSecurity
                            {
                                SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
                                ClientCredentialType = TcpClientCredentialType.Certificate,
                            },
                        },
                  
                    };
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(netTcpBInding, WindowsAuthRelativePath);
                    Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                    builder.ConfigureServiceHostBase<Services.TestService>(serviceHost);
                });
            }

            public void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = new CoreWCF.Description.ServiceCredentials();
                //provide the certificate, here we are getting the default asp.net core default certificate, not recommended for prod workload.
                srvCredentials.ServiceCertificate.Certificate = Helpers.ServiceHelper.GetServiceCertificate();
                srvCredentials.ClientCertificate.Authentication.CertificateValidationMode = CoreWCF.Security.X509CertificateValidationMode.None;

                host.Description.Behaviors.Add(srvCredentials);
            }
        }
    }
}
