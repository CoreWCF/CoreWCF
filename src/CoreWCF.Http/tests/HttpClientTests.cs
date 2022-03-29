using System;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Http.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace CoreWCF.Http.Tests
{
    public class HttpClientTests : IClassFixture<IntegrationTest<HttpClientTests.Startup>>
    {
        private readonly ITestOutputHelper _output;
        private readonly IntegrationTest<Startup> _factory;

        public HttpClientTests(ITestOutputHelper output, IntegrationTest<Startup> factory)
        {
            _output = output;
            _factory = factory;
        }

        [Fact]
        public async Task EchoServiceRespondsSuccessfullyWhenRequestIsValid()
        {
            //
            // FIXME:
            // - Pre-read buffer breaks message parsing when Transfer-Encoding is set to chunked (removes first byte)
            // - When a message is invalid, but no error is thrown, message reply crashes because a null message is passed to the Close channel
            //

            var client = _factory.CreateClient();
            const string action = "http://tempuri.org/IEchoService/Echo";
            
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri("http://localhost:8080/BasicWcfService/basichttp.svc", UriKind.Absolute));
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

            const string requestBody = @"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org/"">
   <s:Header/>
   <s:Body>
      <tem:Echo>
         <tem:text>A</tem:text>
      </tem:Echo>
   </s:Body>
</s:Envelope>";
            
            request.Content = new StringContent(requestBody, Encoding.UTF8, "text/xml");

            // FIXME: Commenting out this line will induce a chunked response, which will break the pre-read message parser
            request.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(requestBody);

            var response = await client.SendAsync(request);
            Assert.True(response.IsSuccessStatusCode);

            var responseBody = await response.Content.ReadAsStringAsync();
            _output.WriteLine(responseBody);

            const string expected = "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                                    "<s:Body>" +
                                    "<EchoResponse xmlns=\"http://tempuri.org/\">" +
                                    "<EchoResult>A</EchoResult>" +
                                    "</EchoResponse>" +
                                    "</s:Body>" +
                                    "</s:Envelope>";

            Assert.Equal(expected, responseBody);
        }

        #region Fixtures

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
                    builder.AddService<EchoService>();
                    builder.AddServiceEndpoint<EchoService, IEchoService>(new BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }

        [ServiceContract]
        public interface IEchoService
        {
            [OperationContract]
            string Echo(string text);
        }

        [DataContract]
        public class EchoMessage
        {
            [DataMember]
            public string Text { get; set; }
        }

        public class EchoService : IEchoService
        {
            public string Echo(string text)
            {
                Console.WriteLine($"Received {text} from client!");
                return text;
            }
        }

        #endregion
    }
}
