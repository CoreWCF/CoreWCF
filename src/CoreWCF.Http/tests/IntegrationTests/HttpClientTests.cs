using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Services;
using Xunit;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace CoreWCF.Http.Tests.IntegrationTests
{
    public class HttpClientTests : IClassFixture<IntegrationTest<HttpClientTests.Startup>>
    {
        private readonly IntegrationTest<Startup> _factory;

        public HttpClientTests(IntegrationTest<Startup> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task BasicHttpClientBadRequestWhenBodyIsEmpty()
        {
            var client = _factory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, new Uri("http://localhost:8080/BasicWcfService/basichttp.svc", UriKind.Absolute));
            var response = await client.SendAsync(request);
            Assert.True(response.StatusCode == HttpStatusCode.BadRequest);
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
                    builder.AddService<EchoService>();
                    builder.AddServiceEndpoint<EchoService, IEchoService>(new BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }
}