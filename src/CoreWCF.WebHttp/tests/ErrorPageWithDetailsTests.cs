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
    public class ErrorPageWithDetailsTests
    {
        private readonly ITestOutputHelper _output;

        public ErrorPageWithDetailsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task CreatesErrorPageWithDetails()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                (HttpStatusCode statusCode, string content) = await HttpHelpers.GetAsync(host.GetHttpBaseAddressUri(), "api/errorpage");

                Assert.Equal(HttpStatusCode.BadRequest, statusCode);
                Assert.False(string.IsNullOrEmpty(content));
                Assert.Contains("<p class=\"heading1\">Request Error</p>", content);
                Assert.Contains("The exception stack trace is", content);
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
                    builder.AddService<Services.ErrorPageWithDetailsService>();
                    builder.AddServiceWebEndpoint<Services.ErrorPageWithDetailsService, ServiceContract.IErrorPageWithDetailsService>("api");
                });
            }
        }
    }
}
