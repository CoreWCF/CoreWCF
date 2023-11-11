// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.UnixDomainSocket.Tests
{
    public class BasicTest
    {
        private readonly ITestOutputHelper _output;
        private string LinuxSocketFilepath = "";
        public const string NoSecurityRelativePath = "/uds.svc/security-none";

        public BasicTest(ITestOutputHelper output)
        {
            _output = output;
            LinuxSocketFilepath = Path.Combine(Path.GetTempPath(), "unix1.txt");
        }

        [Fact]
        public void NoAuthUnixDomainSocket()
        {
            string testString = new string('a', 3000);
            IHost host = Helpers.ServiceHelper.CreateWebHostBuilder<StartUpForUDS>(_output,LinuxSocketFilepath);
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.UnixDomainSocketBinding binding = ClientHelper.GetBufferedModeBinding();
                    var uriBuilder = new UriBuilder()
                    {
                        Scheme = "net.uds",
                        Path = LinuxSocketFilepath
                    };
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(uriBuilder.ToString()));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                catch(Exception ex)
                {

                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [LinuxOnlyFactAttribute]
        public void BasicIdentityOnlyAuthLinux()
        {
            string testString = new string('a', 3000);
            IHost host = Helpers.ServiceHelper.CreateWebHostBuilder<StartupForUnixDomainSocketTransportIdentity>(_output, LinuxSocketFilepath);
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.UnixDomainSocketBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.UnixDomainSocketSecurityMode.Transport);
                    binding.Security.Transport.ClientCredentialType = System.ServiceModel.UnixDomainSocketClientCredentialType.PosixIdentity;

                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri("net.uds://" + LinuxSocketFilepath)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }

            }
        }

        [WindowsOnlyFact]
        public void WindowsAuth()
        {
            string testString = new string('a', 3000);
            IHost host = Helpers.ServiceHelper.CreateWebHostBuilder<StartupForWindowsAuth>(_output, LinuxSocketFilepath);
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.UnixDomainSocketBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.UnixDomainSocketSecurityMode.Transport);
                    binding.Security.Transport.ClientCredentialType = System.ServiceModel.UnixDomainSocketClientCredentialType.Windows;

                    var uriBuilder = new UriBuilder()
                    {
                        Scheme = "net.uds",
                        Path = LinuxSocketFilepath
                    };
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(uriBuilder.ToString()));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                catch (Exception ex)
                {

                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }

            }
        }

        [Fact]
        private void BasicCertAsTransport()
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateWebHostBuilder<StartupForUnixDomainSocketTransportCertificate>(_output, LinuxSocketFilepath);
            using (host)
            {
                host.Start();
                System.ServiceModel.UnixDomainSocketBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.UnixDomainSocketSecurityMode.Transport);
                binding.Security.Transport.ClientCredentialType = System.ServiceModel.UnixDomainSocketClientCredentialType.Certificate;
                var uriBuilder = new UriBuilder()
                {
                    Scheme = "net.uds",
                    Path = LinuxSocketFilepath
                };
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                    new System.ServiceModel.EndpointAddress(uriBuilder.ToString()));

                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None,
                    RevocationMode = X509RevocationMode.NoCheck
                };

                ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.ClientCertificate.Certificate = Helpers.ServiceHelper.GetServiceCertificate(); // this is a fake cert and we are not doing client cert validation
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

        [Theory]
        [InlineData(UnixDomainSocketSecurityMode.Transport, UnixDomainSocketClientCredentialType.PosixIdentity)]
        [InlineData(UnixDomainSocketSecurityMode.TransportCredentialOnly, UnixDomainSocketClientCredentialType.Windows)]
        [InlineData(UnixDomainSocketSecurityMode.TransportCredentialOnly, UnixDomainSocketClientCredentialType.Certificate)]
        private void CheckForSecurityModeCompatibility(UnixDomainSocketSecurityMode securityMode, UnixDomainSocketClientCredentialType clientCredType)
        {

            var udsBinding = new UnixDomainSocketBinding
            {
                Security = new UnixDomainSocketSecurity
                {
                    Mode = securityMode,
                    Transport = new UnixDomainSocketTransportSecurity
                    {
                        ClientCredentialType = clientCredType,
                    },
                },
            };
            Assert.Throws<NotSupportedException>(() => udsBinding.CreateBindingElements());
        }

        public class UDS
        {
            public string GetUDSFilePath()
            {
                return Path.Combine(Path.GetTempPath(), "unix1.txt");
            }
        }

        public class StartUpForUDS : UDS
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IHost host)
            {
                CoreWCF.UnixDomainSocketBinding serverBinding = new CoreWCF.UnixDomainSocketBinding(UnixDomainSocketSecurityMode.None);
                host.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(serverBinding, "net.uds://" + GetUDSFilePath());
                });
            }
        }

        public class StartupForUnixDomainSocketTransportCertificate : UDS
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IHost host)
            {
                host.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    var udsBinding = new UnixDomainSocketBinding
                    {
                        Security = new UnixDomainSocketSecurity
                        {
                            Mode = UnixDomainSocketSecurityMode.Transport,
                            Transport = new UnixDomainSocketTransportSecurity
                            {
                                ClientCredentialType = UnixDomainSocketClientCredentialType.Certificate,
                            },
                        },
                    };

                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(udsBinding, "net.uds://" + GetUDSFilePath());
                    Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                    builder.ConfigureServiceHostBase<Services.TestService>(serviceHost);
                });
            }

            public void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = host.Credentials;
                //provide the certificate, here we are getting the default asp.net core default certificate, not recommended for prod workload.
                srvCredentials.ServiceCertificate.Certificate = Helpers.ServiceHelper.GetServiceCertificate();
                srvCredentials.ClientCertificate.Authentication.CertificateValidationMode = CoreWCF.Security.X509CertificateValidationMode.None;
            }
        }

        public class StartupForUnixDomainSocketTransportIdentity : UDS
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IHost host)
            {
                host.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    var udsBinding = new UnixDomainSocketBinding
                    {
                        Security = new UnixDomainSocketSecurity
                        {
                            Mode = UnixDomainSocketSecurityMode.TransportCredentialOnly,
                            Transport = new UnixDomainSocketTransportSecurity
                            {
                                ClientCredentialType = UnixDomainSocketClientCredentialType.PosixIdentity,
                            },
                        },
                    };

                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(udsBinding, "net.uds://" + GetUDSFilePath());
                });
            }
        }

        public class StartupForWindowsAuth : UDS
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IHost host)
            {
                host.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    var udsBinding = new UnixDomainSocketBinding
                    {
                        Security = new UnixDomainSocketSecurity
                        {
                            Mode = UnixDomainSocketSecurityMode.Transport,
                            Transport = new UnixDomainSocketTransportSecurity
                            {
                                ClientCredentialType = UnixDomainSocketClientCredentialType.Windows,
                            },
                        },
                    };

                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(udsBinding,   "net.uds://" +  GetUDSFilePath());
                });
            }
        }
    }
}
