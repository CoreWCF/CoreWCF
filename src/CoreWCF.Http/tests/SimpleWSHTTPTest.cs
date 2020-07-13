using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Selectors;
using Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace WSHttp
{
    public class SimpleWSHTTPTest
    {
        private ITestOutputHelper _output;

        public SimpleWSHTTPTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void WSHttpRequestReplyEchoString()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<WSHttpNoSecurity>(_output).Build();
            using (host)
            {
                host.Start();
                var wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.None);
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/WSHttpWcfService/basichttp.svc")));
                var channel = factory.CreateChannel();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }
        [Fact]
        public void WSHttpRequestReplyWithTransportMessageEchoString()
        {
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateCertificate);
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithMessageCredential>(_output).Build();
            using (host)
            {
                host.Start();

                
               
                var wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.TransportWithMessageCredential);
                wsHttpBinding.Security.Message.ClientCredentialType = System.ServiceModel.MessageCredentialType.UserName;

                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("https://localhost:8443/WSHttpWcfService/basichttp.svc")));
                ClientCredentials clientCredentials = (ClientCredentials) factory.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                clientCredentials.UserName.UserName = "Administrator";
                clientCredentials.UserName.Password = "fakeone";
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication();
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication.CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None;
                ClientContract.IEchoService channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

        static bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }

        internal class CustomTestValidator : UserNamePasswordValidator
        {
            public override void Validate(string userName, string password)
            {
                return;
            }
        }
        internal class WSHttpTransportWithMessageCredential : StartupWSHttpBase
        {
            public WSHttpTransportWithMessageCredential() : base(CoreWCF.SecurityMode.TransportWithMessageCredential)
            {
            }

        }
        internal class WSHttpNoSecurity : StartupWSHttpBase
        {
            public WSHttpNoSecurity() : base(CoreWCF.SecurityMode.None)
            {
            }

        }
        internal class StartupWSHttpBase
        {
            CoreWCF.SecurityMode wsHttpSecurityMode;
            public StartupWSHttpBase(CoreWCF.SecurityMode securityMode )
            {
                this.wsHttpSecurityMode = securityMode;
            }
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton(typeof(ServiceConfigurationDelegateHolder<>));

            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                CoreWCF.WSHttpBinding serverBinding = new CoreWCF.WSHttpBinding(this.wsHttpSecurityMode);
                serverBinding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
                //TODO move above to WSHttpTransportWithMessageCredential by creating virtual to override binding
                app.UseServiceModel(builder =>
                {
                builder.AddService<Services.EchoService>();
                //  ServiceDescription.GetService()
                builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(serverBinding, "/WSHttpWcfService/basichttp.svc");
                 Action<ServiceHostBase> serviceHost = host =>
                    {
                        var srvCredentials = new CoreWCF.Description.ServiceCredentials();
                        srvCredentials.UserNameAuthentication.UserNamePasswordValidationMode
                            = CoreWCF.Security.UserNamePasswordValidationMode.Custom;
                        srvCredentials.UserNameAuthentication.CustomUserNamePasswordValidator
                            = new CustomTestValidator();
                        host.Description.Behaviors.Add(srvCredentials);
                    };
                    
                    builder.ConfigureService<Services.EchoService>(serviceHost);
                });
            }
        }
    }
}
