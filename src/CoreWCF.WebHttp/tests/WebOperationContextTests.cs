// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.WebHttp.Tests
{
    public class WebOperationContextTests
    {
        private readonly ITestOutputHelper _output;

        public WebOperationContextTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task StatusCodeSet()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                (HttpStatusCode statusCode, string _) = await HttpHelpers.GetAsync(host.GetHttpBaseAddressUri(),"api/statuscode");

                Assert.Equal(HttpStatusCode.Accepted, statusCode);
            }
        }

        [Fact]
        public async Task ResponseHeaderAdded()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                using HttpClient httpClient = new HttpClient();

                HttpResponseMessage response = await httpClient.GetAsync($"http://localhost:{host.GetHttpPort()}/api/responseheader");

                Assert.True(response.Headers.Contains("TestHeader"));
                Assert.Equal("test", string.Join("", response.Headers.GetValues("TestHeader")));
            }
        }

        [Fact]
        public async Task ContentTypeSet()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                using HttpClient httpClient = new HttpClient();

                HttpResponseMessage response = await httpClient.GetAsync($"http://localhost:{host.GetHttpPort()}/api/contenttype");

                Assert.Equal("text/plain", response.Content.Headers.ContentType.ToString());
            }
        }

        [Fact]
        public async Task CanInspectRouteMatch()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                (HttpStatusCode _, string content) = await HttpHelpers.GetAsync(host.GetHttpBaseAddressUri(), "api/match");

                Assert.Equal($"\"http:\\/\\/localhost:{host.GetHttpPort()}\\/api\\/match\"", content);
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelWebServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.WebOperationContextService>();
                    builder.AddServiceWebEndpoint<Services.WebOperationContextService, ServiceContract.IWebOperationContextService>("api");
                });
            }
        }
    }
}
