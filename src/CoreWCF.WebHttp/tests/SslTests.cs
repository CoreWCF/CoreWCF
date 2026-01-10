// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.WebHttp.Tests
{
    public class SslTests
    {
        private readonly ITestOutputHelper _output;

        public SslTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task SslWorks()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilderWithSsl<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                (HttpStatusCode statusCode, string content) = await HttpHelpers.GetAsync(host.GetHttpBaseAddressUri(), "api/hello");
                Assert.Equal(HttpStatusCode.OK, statusCode);
                Assert.Equal("\"hello\"", content);

                (HttpStatusCode sslStatusCode, string sslContent) = await HttpHelpers.GetSslAsync(host.GetHttpsBaseAddressUri(), "api/hello");
                Assert.Equal(HttpStatusCode.OK, sslStatusCode);
                Assert.Equal("\"hello\"", sslContent);
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
                    builder.AddService<Services.SslService>();
                    builder.AddServiceWebEndpoint<Services.SslService, ServiceContract.ISslService>("api");
                    builder.AddServiceWebEndpoint<Services.SslService, ServiceContract.ISslService>(new WebHttpBinding(WebHttpSecurityMode.Transport), "api");
                });
            }
        }
    }
}
