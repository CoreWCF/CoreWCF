// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Selectors;
using Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
#if NETCOREAPP3_1_OR_GREATER
using Microsoft.AspNetCore.Authentication.Negotiate;
#endif

namespace WSHttp
{
    public class SimpleWSHTTPTest
    {
        private readonly ITestOutputHelper _output;

        public SimpleWSHTTPTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact, Description("no-security-with-an-anonymous-client")]
        public void WSHttpRequestReplyEchoString()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<WSHttpNoSecurity>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.None);
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/WSHttpWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

#if NETCOREAPP3_1_OR_GREATER // On NetFx, Negotiate auth is forwarded to Windows auth, but Windows auth on Kestrel is not supported on NetFx
        [Fact, Description("transport-security-with-an-anonymous-client")]
        public void WSHttpRequestReplyEchoStringTransportSecurity()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportSecurityOnly>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.Transport);
                wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.None;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("https://localhost:8443/WSHttpWcfService/basichttp.svc")));
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                Console.WriteLine("read ");
                ((IChannel)channel).Close();
            }
        }
#endif

        [Fact , Description("Demuxer-failure")]
        public void WSHttpRequestReplyWithTransportMessageEchoStringDemuxFailure()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithMessageCredentialWithUserNameExpire>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("https://localhost:8443/WSHttpWcfService/basichttp.svc")));
                ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.UserName.UserName = "testuser@corewcf";
                clientCredentials.UserName.Password = "abab014eba271b2accb05ce0a8ce37335cce38a30f7d39025c713c2b8037d920";
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                Thread.Sleep(6000);
                try
                {
                    channel.EchoString(testString);
                }
                catch(Exception ex)
                {
                    Assert.True(typeof(System.ServiceModel.FaultException).Equals(ex.InnerException.GetType()));
                    Assert.Contains("expired security context token", ex.InnerException.Message);
                }
            }
        }

        [Fact , Description("user-validation-failure")]
        public void WSHttpRequestReplyWithTransportMessageEchoStringUserValidationFailure()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithMessageCredentialWithUserNameExpire>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("https://localhost:8443/WSHttpWcfService/basichttp.svc")));
                ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.UserName.UserName = "invalid-user@corewcf";
                clientCredentials.UserName.Password = "invalid-password";
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientContract.IEchoService channel = factory.CreateChannel();
                try
                {
                    ((IChannel)channel).Open();
                }
                catch(Exception ex)
                {
                    Assert.True(typeof(System.ServiceModel.FaultException).Equals(ex.InnerException.GetType()));
                    Assert.Contains("An error occurred when verifying security for the message.", ex.InnerException.Message);
                }
            }
        }

        [Fact, Description("transport-security-with-basic-authentication")]
        public void WSHttpRequestReplyWithTransportMessageEchoString()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithMessageCredentialWithUserName>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("https://localhost:8443/WSHttpWcfService/basichttp.svc")));
                ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.UserName.UserName = "testuser@corewcf";
                clientCredentials.UserName.Password = "abab014eba271b2accb05ce0a8ce37335cce38a30f7d39025c713c2b8037d920";
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                Thread.Sleep(5000);

                ((IChannel)channel).Close();
                Console.WriteLine("read ");
            }
        }

         [Fact, Description("transport-security-with-certificate-authentication")]
        internal void WSHttpRequestReplyWithTransportMessageCertificateEchoString()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithMessageCredentialWithCertificate>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.Certificate;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("https://localhost:8443/WSHttpWcfService/basichttp.svc")));
                ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.ClientCertificate.Certificate = ServiceHelper.GetServiceCertificate();
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                ((IChannel)channel).Close();
                Console.WriteLine("read ");
            }
        }

#if NETCOREAPP3_1_OR_GREATER // Windows Auth on Kestrel not supported on NetFx
        [Fact, Description("transport-security-with-windows-authentication-kestrel")]
        [Trait("Category", "WindowsOnly")]
        internal void WSHttpRequestImpersonateWithKestrel()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithImpersonation>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.Transport);
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("https://localhost:8443/WSHttpWcfService/basichttp.svc")));
                factory.Credentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                string result = channel.EchoForImpersonation(testString);
                Assert.Equal(testString, result);
                ((IChannel)channel).Close();
            }
        }
#endif

        [Fact(Skip="Temporarily failing in Azure Devops due to corrupted IIS Express dev certificate in test environment image."),
            Description("transport-security-with-windows-authentication-httpsys")]
        [Trait("Category", "WindowsOnly")]  // HttpSys not supported on Linux
        internal void WSHttpRequestImpersonateWithHttpSys()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilderWithHttpSys<WSHttpTransportWithImpersonationHttpSys>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.Transport);
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("https://localhost:44300/WSHttpWcfService/basichttp.svc")));
                factory.Credentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                string result = channel.EchoForImpersonation(testString);
                Assert.Equal(testString, result);
                ((IChannel)channel).Close();
            }
        }

        [Fact, Description("no-security-with-an-anonymous-client-using-impersonation")]
        [Trait("Category", "WindowsOnly")]
        public void WSHttpRequestImpersonateFailsWithoutAuthentication()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<WSHttpNoSecurity>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.None);
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/WSHttpWcfService/basichttp.svc")));
                factory.Credentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientContract.IEchoService channel = factory.CreateChannel();
                Assert.Throws<System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail>>(() =>
                {
                    channel.EchoForImpersonation(testString);
                });
            }
        }

        [Fact(Skip = "Failing in Azure Devops due to corrupted IIS Express dev certificate"),
            Description("no-security-with-an-anonymous-client-using-impersonation-httpsys")]
        [Trait("Category", "WindowsOnly")]  // HttpSys not supported on Linux
        public void WSHttpRequestImpersonateWithHttpSysFailsWithoutAuthentication()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilderWithHttpSys<WSHttpNoSecurityHttpSys>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.None);
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8085/WSHttpWcfService/basichttp.svc")));
                factory.Credentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientContract.IEchoService channel = factory.CreateChannel();
                Assert.Throws<System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail>>(() =>
                {
                    channel.EchoForImpersonation(testString);
                });
            }
        }

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

        internal class WSHttpTransportWithMessageCredentialWithCertificate : StartupWSHttpBase
        {
            public WSHttpTransportWithMessageCredentialWithCertificate() :
                base(SecurityMode.TransportWithMessageCredential, MessageCredentialType.Certificate)
            {
            }

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = new CoreWCF.Description.ServiceCredentials();
                srvCredentials.ClientCertificate.Authentication.CertificateValidationMode
                    = CoreWCF.Security.X509CertificateValidationMode.Custom;
                srvCredentials.ClientCertificate.Authentication.CustomCertificateValidator
                    = new MyX509CertificateValidator();
                srvCredentials.ServiceCertificate.Certificate = ServiceHelper.GetServiceCertificate();
                host.Description.Behaviors.Add(srvCredentials);
            }

            public class MyX509CertificateValidator : X509CertificateValidator
            {
                public MyX509CertificateValidator()
                {
                }

                public override void Validate(X509Certificate2 certificate)
                {
                    // just Check that there is a certificate.
                    if (certificate == null)
                    {
                        throw new ArgumentNullException("certificate");
                    }
                }
            }
        }

        internal class WSHttpTransportWithMessageCredentialWithUserNameExpire : WSHttpTransportWithMessageCredentialWithUserName
        {
            public override CoreWCF.Channels.Binding ChangeBinding(WSHttpBinding binding)
            {
               CoreWCF.Channels.CustomBinding customBinding = new CoreWCF.Channels.CustomBinding(binding);
               CoreWCF.Channels.SecurityBindingElement security = customBinding.Elements.Find<CoreWCF.Channels.SecurityBindingElement>();
               security.LocalServiceSettings.InactivityTimeout = TimeSpan.FromSeconds(3);
               return customBinding;
            }
        }

        internal class WSHttpTransportWithMessageCredentialWithUserName : StartupWSHttpBase
        {
            public WSHttpTransportWithMessageCredentialWithUserName() :
                base(SecurityMode.TransportWithMessageCredential, MessageCredentialType.UserName)
            {
            }

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = new CoreWCF.Description.ServiceCredentials();
                srvCredentials.UserNameAuthentication.UserNamePasswordValidationMode
                    = CoreWCF.Security.UserNamePasswordValidationMode.Custom;
                srvCredentials.UserNameAuthentication.CustomUserNamePasswordValidator
                    = new CustomTestValidator();
                host.Description.Behaviors.Add(srvCredentials);
            }
        }

        internal class WSHttpTransportSecurityOnly : StartupWSHttpBase
        {
            public WSHttpTransportSecurityOnly() : base(SecurityMode.Transport, MessageCredentialType.None)
            {
            }

            protected override AuthenticationBuilder AddAuthenticationService(IServiceCollection services)
            {
#if NET472
                return services.AddAuthentication(HttpSysDefaults.AuthenticationScheme)
                    .AddPolicyScheme("Negotiate", "Negotiate", options =>
                    {
                        options.ForwardDefault = HttpSysDefaults.AuthenticationScheme;
                    });
#else
                return services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                    .AddNegotiate();
#endif
            }
        }

        internal class WSHttpNoSecurityHttpSys : WSHttpNoSecurity
        {
            protected override AuthenticationBuilder AddAuthenticationService(IServiceCollection services)
            {
                return services.AddAuthentication(HttpSysDefaults.AuthenticationScheme)
                    .AddPolicyScheme("Negotiate", "Negotiate", options =>
                    {
                        options.ForwardDefault = HttpSysDefaults.AuthenticationScheme;
                    });
            }
        }

        internal class WSHttpNoSecurity : StartupWSHttpBase
        {
            public WSHttpNoSecurity() : base(SecurityMode.None, MessageCredentialType.None)
            {
            }
        }

        internal class WSHttpTransportWithImpersonationHttpSys : WSHttpTransportWithImpersonation
        {
            protected override AuthenticationBuilder AddAuthenticationService(IServiceCollection services)
            {
#if NET472
                return services.AddAuthentication(HttpSysDefaults.AuthenticationScheme)
                    .AddPolicyScheme("Negotiate", "Negotiate", options =>
                    {
                        options.ForwardDefault = HttpSysDefaults.AuthenticationScheme;
                    });
#else
                return services.AddAuthentication(HttpSysDefaults.AuthenticationScheme)
                    .AddNegotiate();
#endif
            }
        }

        internal class WSHttpTransportWithImpersonation : StartupWSHttpBase
        {
            public WSHttpTransportWithImpersonation() :
                base(SecurityMode.Transport, MessageCredentialType.None)
            {
            }

            public override CoreWCF.Channels.Binding ChangeBinding(WSHttpBinding wsBinding)
            {
                wsBinding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Windows;
                return wsBinding;
            }
        }

        internal abstract class StartupWSHttpBase
        {
            private readonly CoreWCF.SecurityMode _wsHttpSecurityMode;
            private readonly MessageCredentialType _credentialType;
            public StartupWSHttpBase(CoreWCF.SecurityMode securityMode, MessageCredentialType credentialType)
            {
                _wsHttpSecurityMode = securityMode;
                _credentialType = credentialType;
            }
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                AddAuthenticationService(services);
            }

            protected virtual AuthenticationBuilder AddAuthenticationService(IServiceCollection services)
            {
#if NET472
                return services.AddAuthentication(HttpSysDefaults.AuthenticationScheme);
#else
                return services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                    .AddNegotiate(); 
#endif
            }

            public virtual void ChangeHostBehavior(ServiceHostBase host)
            {

            }

            public virtual CoreWCF.Channels.Binding ChangeBinding(WSHttpBinding wsBInding)
            {
                return wsBInding;
            }
           
            public void Configure(IApplicationBuilder app)
            {
                CoreWCF.WSHttpBinding serverBinding = new CoreWCF.WSHttpBinding(_wsHttpSecurityMode);
                serverBinding.Security.Message.ClientCredentialType = _credentialType;
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(ChangeBinding(serverBinding), "/WSHttpWcfService/basichttp.svc");
                    Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                    builder.ConfigureServiceHostBase<Services.EchoService>(serviceHost);
                });
            }
        }
    }
}
