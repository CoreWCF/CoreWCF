﻿using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class HttpsService
    {
        private ITestOutputHelper _output;

        public HttpsService(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicHttpsRequestReplyEchoString()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateHttpsWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpsBinding = ClientHelper.GetBufferedModeHttpsBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpsBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("https://localhost:8443/BasicWcfService/basichttp.svc")));
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication();
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication.CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None;
                var channel = factory.CreateChannel();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }
}
