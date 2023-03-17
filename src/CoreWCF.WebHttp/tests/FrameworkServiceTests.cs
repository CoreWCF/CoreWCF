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

            (HttpStatusCode statusCode, string content) = await HttpHelpers.GetAsync("api/async/get");
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

            (HttpStatusCode statusCode, string content) = await HttpHelpers.PostJsonAsync("api/async/post", new ServiceContract.AsyncData { Data = "async" });
            ServiceContract.AsyncData responseData = SerializationHelpers.DeserializeJson<ServiceContract.AsyncData>(content);

            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Equal("async", responseData.Data);
        }
    }

    [Fact]
    public void ImplicitlySetFormatIsCopiedCorrectly()
    {
        var method = typeof(IFrameworkService).GetMethod(nameof(IFrameworkService.ImplicitlySetFormat));

        var webGetAttribute = WebHttpServiceModelCompat.GetNativeAttribute<CoreWCF.Web.WebGetAttribute>(method!)!;

        Assert.False(webGetAttribute.IsRequestFormatSetExplicitly, $"{nameof(Web.WebGetAttribute.IsRequestFormatSetExplicitly)} was not correctly copied");
        Assert.False(webGetAttribute.IsResponseFormatSetExplicitly, $"{nameof(Web.WebGetAttribute.IsResponseFormatSetExplicitly)} was not correctly copied");
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
                builder.AddServiceWebEndpoint<Services.FrameworkService, ServiceContract.IFrameworkService>("api");
            });
        }
    }
}
#endif
