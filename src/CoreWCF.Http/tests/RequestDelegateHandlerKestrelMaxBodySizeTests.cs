// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Hosting;
using Helpers;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace CoreWCF.Http.Tests
{
    public class RequestDelegateHandlerKestrelMaxBodySizeTests
    {
        private readonly ITestOutputHelper _output;

        public RequestDelegateHandlerKestrelMaxBodySizeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Kestrel_MaxRequestBodySize_Increased_When_Lower_Than_Binding()
        {
            var builder = ServiceHelper.CreateWebHostBuilder<Startup_Kestrel_MaxRequestBodySize_Increased_When_Lower_Than_Binding>(_output);
            builder.ConfigureServices(services =>
            {
                services.Configure<KestrelServerOptions>(options =>
                {
                    options.Limits.MaxRequestBodySize = 10;
                });
            });
            IWebHost host = builder.Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> channelFactory = null;
                channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(ClientHelper.GetBufferedModeBinding(),
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/TestService/basichttp.svc")));
                var channel = channelFactory.CreateChannel();
                string testString = new('a', 100);
                string result = channel.EchoString(testString);
                channelFactory.Close();
            }
        }

        [Fact]
        public void Kestrel_MaxRequestBodySize_Not_Decreased_When_Higher_Than_Binding()
        {
            var builder = ServiceHelper.CreateWebHostBuilder<Startup_Kestrel_MaxRequestBodySize_Increased_When_Lower_Than_Binding>(_output);
            builder.ConfigureServices(services =>
            {
                services.Configure<KestrelServerOptions>(options =>
                {
                    options.Limits.MaxRequestBodySize = 100_000;
                });
            });
            IWebHost host = builder.Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> channelFactory = null;
                channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(ClientHelper.GetBufferedModeBinding(),
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/TestService/basichttp.svc")));
                var channel = channelFactory.CreateChannel();
                string testString = new('a', 100);
                string result = channel.EchoString(testString);
                channelFactory.Close();
            }
        }

        internal class Startup_Kestrel_MaxRequestBodySize_Increased_When_Lower_Than_Binding
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/TestService/basichttp.svc");
                });
                app.Use(async (context, next) =>
                {
                    var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                    Assert.Equal(65536, feature.MaxRequestBodySize);
                    await next();
                });
            }
        }

        internal class Startup_Kestrel_MaxRequestBodySize_Not_Decreased_When_Higher_Than_Binding
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/TestService/basichttp.svc");
                });
                app.Use(async (context, next) =>
                {
                    var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
                    Assert.Equal(100_000, feature.MaxRequestBodySize);
                    await next();
                });
            }
        }
    }
}
