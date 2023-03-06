// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using ClientContract;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Http.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class ServiceWithCoreWCFXmlSerializerFormatTest : IClassFixture<IntegrationTest<ServiceWithCoreWCFXmlSerializerFormatTest.Startup>>
    {
        private readonly ITestOutputHelper _output;
        private readonly IntegrationTest<Startup> _factory;

        public ServiceWithCoreWCFXmlSerializerFormatTest(ITestOutputHelper output, IntegrationTest<Startup> factory)
        {
            _output = output;
            _factory = factory;
        }

        [Fact]
        public async Task BasicScenarioServiceMessageParameter()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IServiceWithCoreWCFXmlSerializerFormat>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/Service.svc")));
                IServiceWithCoreWCFXmlSerializerFormat channel = factory.CreateChannel();


                var result = channel.Identity("test");
                Assert.Equal("test", result);
                ((IChannel)channel).Close();
            }
        }

        [Fact]
        public async Task BasicScenarioServiceMessageParameterWithHttpClient()
        {
            var client = _factory.CreateClient();
            const string action = "http://tempuri.org/IServiceWithCoreWCFXmlSerializerFormat/Identity";

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri($"http://localhost:{_factory.GetHttpPort()}/BasicWcfService/Service.svc", UriKind.Absolute));
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

            const string requestBody = @"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org/"">
   <s:Header/>
   <s:Body>
      <tem:Identity>
         <tem:msg>A</tem:msg>
      </tem:Identity>
   </s:Body>
</s:Envelope>";

            request.Content = new StringContent(requestBody, Encoding.UTF8, "text/xml");

            //// FIXME: Commenting out this line will induce a chunked response, which will break the pre-read message parser
            request.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(requestBody);

            var response = await client.SendAsync(request);
            Assert.True(response.IsSuccessStatusCode);

            var responseBody = await response.Content.ReadAsStringAsync();
            _output.WriteLine(responseBody);

            const string expected =
                "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "<s:Body xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\">" +
                "<IdentityResponse xmlns=\"http://tempuri.org/\">" +
                "<IdentityResult>A</IdentityResult>" +
                "</IdentityResponse>" +
                "</s:Body>" +
                "</s:Envelope>";

            Assert.Equal(expected, responseBody);
        }

        public class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.ServiceWithCoreWCFXmlSerializerFormat>();
                    builder.AddServiceEndpoint<Services.ServiceWithCoreWCFXmlSerializerFormat, Services.IServiceWithCoreWCFXmlSerializerFormat>(new BasicHttpBinding(), "/BasicWcfService/Service.svc");
                });
            }
        }
    }
}
