// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class HttpsService
    {
        private readonly ITestOutputHelper _output;

        public HttpsService(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicHttpsRequestReplyEchoString()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpsBinding httpsBinding = ClientHelper.GetBufferedModeHttpsBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpsBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"https://localhost:{host.GetHttpsPort()}/BasicWcfService/basichttp.svc")));
                factory.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                {
                    CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                };
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(BasicHttpSecurityMode.Transport), "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }
}
