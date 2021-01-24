// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Threading;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Selectors;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

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

        [Fact, Description("transport-security-with-an-anonymous-client")]
        public void WSHttpRequestReplyEchoStringTransportSecurity()
        {
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateCertificate);
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

        [Fact, Description("transport-security-with-basic-authentication")]
        public void WSHttpRequestReplyWithTransportMessageEchoString()
        {
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateCertificate);
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

        // [Fact, Description("transport-security-with-certificate-authentication")]
        //TODO set up in container, tested locally and this works
        internal void WSHttpRequestReplyWithTransportMessageCertificateEchoString()
        {
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateCertificate);
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
                clientCredentials.ClientCertificate.SetCertificate(
                StoreLocation.LocalMachine,
                StoreName.My, X509FindType.FindBySubjectName
                , "birojtestcert"
                );

                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                ((IChannel)channel).Close();
                Console.WriteLine("read ");
            }
        }

        private static bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }

        internal class CustomTestValidator : UserNamePasswordValidator
        {
            public override void Validate(string userName, string password)
            {
                if (string.Compare(userName, "testuser@corewcf", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return;
                }
                else
                {
                    throw new Exception("Permission Denied");
                }
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
                    = CoreWCF.Security.X509CertificateValidationMode.PeerOrChainTrust;
                srvCredentials.ClientCertificate.Authentication.TrustedStoreLocation = StoreLocation.LocalMachine;
                srvCredentials.ServiceCertificate.SetCertificate(
                    StoreLocation.LocalMachine,
                    StoreName.Root, X509FindType.FindBySubjectName
                    , "birojtestcert"
                    );
                host.Description.Behaviors.Add(srvCredentials);
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

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                //nothing to do
            }
        }

        internal class WSHttpTransportSecurityWindowsAuth : StartupWSHttpBase
        {
            public WSHttpTransportSecurityWindowsAuth() : base(CoreWCF.SecurityMode.TransportWithMessageCredential, MessageCredentialType.Windows)
            {
            }

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                //nothing to do
            }
        }

        internal class WSHttpNoSecurity : StartupWSHttpBase
        {
            public WSHttpNoSecurity() : base(SecurityMode.None, MessageCredentialType.None)
            {
            }

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                //nothing to do
            }
        }

        internal abstract class StartupWSHttpBase
        {
            private readonly CoreWCF.SecurityMode wsHttpSecurityMode;
            private readonly MessageCredentialType credentialType;
            public StartupWSHttpBase(CoreWCF.SecurityMode securityMode, MessageCredentialType credentialType)
            {
                wsHttpSecurityMode = securityMode;
                this.credentialType = credentialType;
            }
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public abstract void ChangeHostBehavior(ServiceHostBase host);

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                CoreWCF.WSHttpBinding serverBinding = new CoreWCF.WSHttpBinding(wsHttpSecurityMode);
                serverBinding.Security.Message.ClientCredentialType = credentialType;
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(serverBinding, "/WSHttpWcfService/basichttp.svc");
                    Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                    builder.ConfigureServiceHostBase<Services.EchoService>(serviceHost);
                });
            }
        }
    }
}
