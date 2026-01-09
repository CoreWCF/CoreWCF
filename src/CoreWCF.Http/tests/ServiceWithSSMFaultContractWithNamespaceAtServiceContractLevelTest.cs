// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class ServiceWithSSMFaultContractWithNamespaceAtServiceContractLevelTest : IClassFixture<IntegrationTest<ServiceWithSSMFaultContractWithNamespaceAtServiceContractLevelTest.Startup>>
    {
        private readonly ITestOutputHelper _output;
        private readonly IntegrationTest<Startup> _factory;

        public ServiceWithSSMFaultContractWithNamespaceAtServiceContractLevelTest(ITestOutputHelper output, IntegrationTest<Startup> factory)
        {
            _output = output;
            _factory = factory;
        }

        [Fact]
        public void BasicScenarioServiceWithSSMFaultContract()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IServiceWithSSMFaultContractWithNamespaceAtServiceContractLevel>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/Service.svc")));
                IServiceWithSSMFaultContractWithNamespaceAtServiceContractLevel channel = factory.CreateChannel();

                var e = Assert.Throws<System.ServiceModel.FaultException<ClientContract.SSMCompatibilityFault>>(() => channel.Identity("test"));

                ((IChannel)channel).Close();
            }
        }

        [Fact]
        public async Task BasicScenarioServiceWithSSMFaultContractWithHttpClient()
        {
            var client = _factory.CreateClient();
            const string action = "https://ssm-fault-contract-compatibility.com/IServiceWithSSMFaultContractWithNamespaceAtServiceContractLevel/Identity";

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri("http://localhost:8080/BasicWcfService/Service.svc", UriKind.Absolute));
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

            const string requestBody = @"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
   <s:Header/>
   <s:Body>
      <Identity xmlns=""https://ssm-fault-contract-compatibility.com"">
         <Input>A</Input>
      </Identity>
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
                + "<faultcode>s:Client</faultcode>"
                + "<faultstring"
                + $"{GetXmlLangAttributeOrNot()}"
                + ">The creator of this fault did not specify a Reason.</faultstring>"
                + "<detail>"
                + "<SSMCompatibilityFault xmlns=\"https://ssm-fault-contract-compatibility.com\" xmlns:a=\"http://schemas.datacontract.org/2004/07/Services\" xmlns:i=\"http://www.w3.org/2001/XMLSchema-instance\">"
                + "<a:Message>An error occured</a:Message></SSMCompatibilityFault></detail></s:Fault></s:Body></s:Envelope>";

            Assert.Equal(expected, responseBody);

            string GetXmlLangAttributeOrNot() => CultureInfo.CurrentCulture.Name.Length == 0
                    ? string.Empty
                    : $@" xml:lang=""{CultureInfo.CurrentCulture.Name}""";
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
                    builder.AddService<Services.ServiceWithSSMFaultContractWithNamespaceAtServiceContractLevel>();
                    builder.AddServiceEndpoint<Services.ServiceWithSSMFaultContractWithNamespaceAtServiceContractLevel, Services.IServiceWithSSMFaultContractWithNamespaceAtServiceContractLevel>(new BasicHttpBinding(), "/BasicWcfService/Service.svc");
                });
            }
        }
    }
}
