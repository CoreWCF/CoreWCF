// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Web;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Xunit;
using Xunit.Abstractions;

#if NETFRAMEWORK
namespace CoreWCF.WebHttp.Tests;

public class FrameworkServiceTests
{
    private readonly ITestOutputHelper _output;

    public FrameworkServiceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task AsyncWebGetWorks()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            await host.StartAsync();

            (HttpStatusCode statusCode, string content) = await HttpHelpers.GetAsync(host.GetHttpBaseAddressUri(),"api/async/get");
            ServiceContract.AsyncData responseData = SerializationHelpers.DeserializeJson<ServiceContract.AsyncData>(content);

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Equal("async", responseData.Data);
        }
    }

    [Fact]
    public async Task AsyncWebInvokeWorks()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            await host.StartAsync();

            (HttpStatusCode statusCode, string content) = await HttpHelpers.PostJsonAsync(host.GetHttpBaseAddressUri(),"api/async/post", new ServiceContract.AsyncData { Data = "async" });
            ServiceContract.AsyncData responseData = SerializationHelpers.DeserializeJson<ServiceContract.AsyncData>(content);

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Equal("async", responseData.Data);
        }
    }

    [Fact]
    public async Task CanUseJsonWithFormatNotSetExplicitly()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            await host.StartAsync();

            using var client = new HttpClient { BaseAddress = host.GetHttpBaseAddressUri() };
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/implicitFormat");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await client.SendAsync(request);

            Assert.Equal("application/json", response.Content.Headers.ContentType.MediaType);
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
                builder.AddService<Services.FrameworkService>();
                builder.AddServiceWebEndpoint<Services.FrameworkService, IFrameworkService>("api", behavior =>
                {
                    behavior.DefaultOutgoingResponseFormat = WebMessageFormat.Json;
                });
            });
        }
    }
}
#endif
