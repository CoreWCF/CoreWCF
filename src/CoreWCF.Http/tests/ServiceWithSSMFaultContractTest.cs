// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Http;
using System.Runtime.InteropServices;
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
    public class ServiceWithSSMFaultContractTest : IClassFixture<IntegrationTest<ServiceWithSSMFaultContractTest.Startup>>
    {
        private readonly ITestOutputHelper _output;
        private readonly IntegrationTest<Startup> _factory;

        public ServiceWithSSMFaultContractTest(ITestOutputHelper output, IntegrationTest<Startup> factory)
        {
            _output = output;
            _factory = factory;
        }

        [Fact]
        public void BasicScenarioServiceWithSSMFaultContract()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IServiceWithSSMFaultContract>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/Service.svc")));
                IServiceWithSSMFaultContract channel = factory.CreateChannel();

                var e = Assert.Throws<System.ServiceModel.FaultException<ClientContract.SSMCompatibilityFault>>(() => channel.Identity("test"));

                ((IChannel)channel).Close();
            }
        }

        [Fact]
        [UseCulture("en-US")]
        public async Task BasicScenarioServiceWithSSMFaultContractWithHttpClient()
        {
            var client = _factory.CreateClient();
            const string action = "http://tempuri.org/IServiceWithSSMFaultContract/Identity";

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri("http://localhost:8080/BasicWcfService/Service.svc", UriKind.Absolute));
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

            const string requestBody = @"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org/"">
   <s:Header/>
   <s:Body>
      <tem:Identity>
         <tem:Input>A</tem:Input>
      </tem:Identity>
   </s:Body>
</s:Envelope>";

            request.Content = new StringContent(requestBody, Encoding.UTF8, "text/xml");

            //// FIXME: Commenting out this line will induce a chunked response, which will break the pre-read message parser
            request.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(requestBody);

            var response = await client.SendAsync(request);
            Assert.False(response.IsSuccessStatusCode);

            var responseBody = await response.Content.ReadAsStringAsync();
            //_output.WriteLine(responseBody);

            string expected = "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                "<s:Body><s:Fault>"
                +"<faultcode>s:Client</faultcode>"
                +"<faultstring"
                +$"{GetXmlLangAttributeOrNot()}"
                +">The creator of this fault did not specify a Reason.</faultstring>"
                +"<detail>"
                +"<SSMCompatibilityFault xmlns=\"https://ssm-fault-contract-compatibility.com\" xmlns:a=\"http://schemas.datacontract.org/2004/07/Services\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\">"
                +"<a:Message>An error occured</a:Message></SSMCompatibilityFault></detail></s:Fault></s:Body></s:Envelope>";

            Assert.Equal(expected, responseBody);

            string GetXmlLangAttributeOrNot()
            {
                const string enUsXmlLang = " xml:lang=\"en-US\"";
#if NETCOREAPP3_1_OR_GREATER
                return RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ?
                    string.Empty
                    : enUsXmlLang;
#else
                return enUsXmlLang;
#endif
            }
        }

        public class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.ServiceWithSSMFaultContract>();
                    builder.AddServiceEndpoint<Services.ServiceWithSSMFaultContract, Services.IServiceWithSSMFaultContract>(new BasicHttpBinding(), "/BasicWcfService/Service.svc");
                });
            }
        }
    }
}
