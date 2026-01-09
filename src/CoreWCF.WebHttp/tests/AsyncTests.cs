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
    public class AsyncTests
    {
        private readonly ITestOutputHelper _output;

        public AsyncTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task AsyncWebGetWorks()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                (HttpStatusCode statusCode, string content) = await HttpHelpers.GetAsync(host.GetHttpBaseAddressUri(), "api/async/get");
                ServiceContract.AsyncData responseData = SerializationHelpers.DeserializeJson<ServiceContract.AsyncData>(content);

                Assert.Equal(HttpStatusCode.OK, statusCode);
                Assert.Equal("async", responseData.Data);
            }
        }

        [Fact]
        public async Task AsyncWebInvokeWorks()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                (HttpStatusCode statusCode, string content) = await HttpHelpers.PostJsonAsync(host.GetHttpBaseAddressUri(), "api/async/post", new ServiceContract.AsyncData { Data = "async" });
                ServiceContract.AsyncData responseData = SerializationHelpers.DeserializeJson<ServiceContract.AsyncData>(content);

                Assert.Equal(HttpStatusCode.OK, statusCode);
                Assert.Equal("async", responseData.Data);
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
                    builder.AddService<Services.AsyncService>();
                    builder.AddServiceWebEndpoint<Services.AsyncService, ServiceContract.IAsyncService>("api");
                });
            }
        }
    }
}
