// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using BasicHttp;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests;

public partial class InjectedIncomingPropertiesTests
{
    private readonly ITestOutputHelper _output;

    public InjectedIncomingPropertiesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BasicHttpRequestReplyEchoString()
    {
        string testString = new('a', 3000);
        IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IInjectedIncomingPropertiesService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
            IInjectedIncomingPropertiesService channel = factory.CreateChannel();
            string result = channel.Echo(testString);
            Assert.Equal(testString, result);
        }
    }

    internal class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
            services.AddServiceModelMetadata();
            services.AddSingleton<InjectedIncomingPropertiesService>();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<InjectedIncomingPropertiesService>(options =>
                {
                    options.DebugBehavior.IncludeExceptionDetailInFaults = true;
                });
                builder.AddServiceEndpoint<InjectedIncomingPropertiesService, IInjectedIncomingPropertiesService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
            });
        }
    }

    [System.ServiceModel.ServiceContract]
    internal interface IInjectedIncomingPropertiesService
    {
        [System.ServiceModel.OperationContract]
        string Echo(string value);
    }

    internal partial class InjectedIncomingPropertiesService : IInjectedIncomingPropertiesService
    {
        public string Echo(string value, [Injected] HttpContext httpContext,
            [Injected(PropertyName = HttpRequestMessageProperty.Name)] HttpRequestMessageProperty httpRequestMessageProperty)
        {
            Assert.NotNull(httpContext);
            Assert.NotNull(httpRequestMessageProperty);
            return value;
        }
    }
}
