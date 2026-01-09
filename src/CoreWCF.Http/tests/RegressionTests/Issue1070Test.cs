// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Description;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Security;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.RegressionTests
{
    public class Issue1070Test
    {
        private readonly ITestOutputHelper _output;

        public Issue1070Test(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ThrowingCustomUserNameValidatorReturnsMessageSecurityFault()
        {
            IHost host = ServiceHelper.CreateHttpsWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.Channels.CustomBinding customBinding = new();
                customBinding.Elements.Add(System.ServiceModel.Channels.SecurityBindingElement.CreateUserNameOverTransportBindingElement());
                customBinding.Elements.Add(new System.ServiceModel.Channels.TextMessageEncodingBindingElement());
                customBinding.Elements.Add(new System.ServiceModel.Channels.HttpsTransportBindingElement());
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(customBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/EchoService.svc")));
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientCredentials clientCredentials = factory.Credentials;
                clientCredentials.UserName.UserName = "UserName";
                clientCredentials.UserName.Password = "Password";
                ClientContract.IEchoService channel = factory.CreateChannel();
                ((System.ServiceModel.Channels.IChannel)channel).Open();
                await Assert.ThrowsAsync<System.ServiceModel.Security.MessageSecurityException>(() => channel.EchoStringAsync("test"));
                ServiceHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
                factory.Close();
            }
        }

        private class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    var encoding = new TextMessageEncodingBindingElement();
                    var httpsTransport = new HttpsTransportBindingElement();
                    var security = SecurityBindingElement.CreateUserNameOverTransportBindingElement();
                    security.AllowInsecureTransport = true;

                    var customBinding = new CustomBinding();
                    customBinding.Elements.Add(security);
                    customBinding.Elements.Add(encoding);
                    customBinding.Elements.Add(httpsTransport);

                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(customBinding, "/EchoService.svc");

                    builder.ConfigureServiceHostBase<Services.EchoService>(serviceHost =>
                    {
                        var srvCredentials = serviceHost.Credentials;
                        srvCredentials.UserNameAuthentication.UserNamePasswordValidationMode = UserNamePasswordValidationMode.Custom;
                        srvCredentials.UserNameAuthentication.CustomUserNamePasswordValidator = new CustomUserNameValidator();
                        srvCredentials.ClientCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.None;
                    });
                });

            }

            internal class CustomUserNameValidator : CoreWCF.IdentityModel.Selectors.UserNamePasswordValidator
            {
                public override ValueTask ValidateAsync(string userName, string password)
                {
                    throw new MessageSecurityException("Unknown Username or Incorrect Password");
                }
            }
        }
    }
}
