// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.WebHttp.Tests
{
    public class ErrorPageNoDetailsTests
    {
        private readonly ITestOutputHelper _output;

        public ErrorPageNoDetailsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task CreatesErrorPageNoDetails()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();

                (HttpStatusCode statusCode, string content) = await HttpHelpers.GetAsync("api/errorpage");

                Assert.Equal(HttpStatusCode.BadRequest, statusCode);
                Assert.False(string.IsNullOrEmpty(content));
                Assert.Contains("<p class=\"heading1\">Request Error</p>", content);
                Assert.DoesNotContain("The exception stack trace is", content);
            }
        }

        [Fact(Skip = "TDD Test")]
        public async Task CircularReferenceGivesError()
        {
            using var host = ServiceHelper.CreateWebHostBuilder<GenericWebServiceStartup>()
                .WithServiceBuilder(builder =>
                {
                    builder.AddService<BrokenService>()
                        .AddServiceWebEndpoint<BrokenService, IBrokenServiceContract>("broken");
                })
                .Build();
            await host.StartAsync();

            (HttpStatusCode statusCode, string content) = await HttpHelpers.GetAsync("broken/JsonCircularGraph");

            Assert.InRange((int)statusCode, 500, 599);
        }

        [Fact(Skip = "TDD Test")]
        public async Task JsonReferenceGivesError()
        {
            using var host = ServiceHelper.CreateWebHostBuilder<GenericWebServiceStartup>()
                .WithServiceBuilder(builder =>
                {
                    builder.AddService<BrokenService>()
                        .AddServiceWebEndpoint<BrokenService, IBrokenServiceContract>("broken");
                })
                .Build();
            await host.StartAsync();

            (HttpStatusCode statusCode, string content) = await HttpHelpers.GetAsync("broken/JsonReference");

            Assert.InRange((int)statusCode, 500, 599);
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
                    builder.AddService<Services.ErrorPageNoDetailsService>();
                    builder.AddServiceWebEndpoint<Services.ErrorPageNoDetailsService, ServiceContract.IErrorPageNoDetailsService>("api");
                });
            }
        }
    }
}
