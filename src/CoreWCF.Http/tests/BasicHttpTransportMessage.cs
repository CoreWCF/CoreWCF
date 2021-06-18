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

namespace BasicHttp
{
    public class SimpleBasicHttpTest
    {
        private readonly ITestOutputHelper _output;

        public SimpleBasicHttpTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact, Description("transport-security-with-basic-authentication")]
        public void BasicHttpRequestReplyWithTransportMessageEchoString()
        {
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateCertificate);
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<BasicHttpTransportWithMessageCredentialWithUserName>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding BasicHttpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.BasicHttpSecurityMode.TransportWithMessageCredential);
                BasicHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.BasicHttpMessageCredentialType.UserName;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(BasicHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("https://localhost:8443/BasicHttpWcfService/basichttp.svc")));
                ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.UserName.UserName = "testuser@corewcf";
                clientCredentials.UserName.Password = "abab014eba271b2accb05ce0a8ce37335cce38a30f7d39025c713c2b8037d920";
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
       // TODO set up in container, tested locally and this works
        internal void BasicHttpRequestReplyWithTransportMessageCertificateEchoString()
        {
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateCertificate);
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<BasicHttpTransportWithMessageCredentialWithCertificate>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding BasicHttpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.BasicHttpSecurityMode.TransportWithMessageCredential);
                BasicHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.BasicHttpMessageCredentialType.Certificate;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(BasicHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("https://localhost:8443/BasicHttpWcfService/basichttp.svc")));
                ClientCredentials clientCredentials = (ClientCredentials)factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.ClientCertificate.SetCertificate(
                StoreLocation.CurrentUser,
                StoreName.My, X509FindType.FindBySerialNumber
                , "16437c0e611928da"
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

        internal class BasicHttpTransportWithMessageCredentialWithCertificate : StartupBasicHttpBase
        {
            public BasicHttpTransportWithMessageCredentialWithCertificate() :
                base(CoreWCF.Channels.BasicHttpSecurityMode.TransportWithMessageCredential, BasicHttpMessageCredentialType.Certificate)
            {
            }

            public override void ChangeHostBehavior(ServiceHostBase host)
            {
                var srvCredentials = new CoreWCF.Description.ServiceCredentials();
                srvCredentials.ClientCertificate.Authentication.CertificateValidationMode
                    = CoreWCF.Security.X509CertificateValidationMode.PeerOrChainTrust;
                srvCredentials.ClientCertificate.Authentication.TrustedStoreLocation = StoreLocation.CurrentUser;
                srvCredentials.ServiceCertificate.SetCertificate(
                    StoreLocation.CurrentUser,
                    StoreName.My, X509FindType.FindBySerialNumber
                    , "16437c0e611928da"
                    );
                host.Description.Behaviors.Add(srvCredentials);
            }
        }

        internal class BasicHttpTransportWithMessageCredentialWithUserName : StartupBasicHttpBase
        {
            public BasicHttpTransportWithMessageCredentialWithUserName() :
                base(CoreWCF.Channels.BasicHttpSecurityMode.TransportWithMessageCredential,BasicHttpMessageCredentialType.UserName)
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

        internal abstract class StartupBasicHttpBase
        {
            private readonly CoreWCF.Channels.BasicHttpSecurityMode  _basicHttpSecurityMode;
            private readonly BasicHttpMessageCredentialType _credentialType;
            public StartupBasicHttpBase(CoreWCF.Channels.BasicHttpSecurityMode securityMode, BasicHttpMessageCredentialType credentialType)
            {
                _basicHttpSecurityMode = securityMode;
                _credentialType = credentialType;
            }
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public virtual void ChangeHostBehavior(ServiceHostBase host)
            {

            }

            public virtual CoreWCF.Channels.Binding ChangeBinding(BasicHttpBinding basicBinding)
            {
                return basicBinding;
            }
           
            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                CoreWCF.BasicHttpBinding serverBinding = new CoreWCF.BasicHttpBinding(_basicHttpSecurityMode);
                serverBinding.Security.Message.ClientCredentialType = _credentialType;
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(ChangeBinding(serverBinding), "/BasicHttpWcfService/basichttp.svc");
                    Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                    builder.ConfigureServiceHostBase<Services.EchoService>(serviceHost);
                });
            }
        }
    }
}
