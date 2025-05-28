// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using CoreWCF.Http.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    [Collection(HttpSysTestCollectionDefinition.HttpSysTestCollection)]
    public partial class RequestDelegateHandlerMaxBodySizeTests
    {
        private readonly ITestOutputHelper _output;

        public RequestDelegateHandlerMaxBodySizeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Kestrel_MaxRequestBodySize_Increased_When_Lower_Than_Binding()
        {
            var builder = ServiceHelper.CreateWebHostBuilder<Startup>(_output);
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
                System.ServiceModel.ChannelFactory<IHttpMaxRequestBodySizeService> channelFactory = null;
                channelFactory = new System.ServiceModel.ChannelFactory<IHttpMaxRequestBodySizeService>(ClientHelper.GetBufferedModeBinding(),
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/TestService/basichttp.svc")));
                var channel = channelFactory.CreateChannel();
                Assert.Equal(65536, channel.GetMaxRequestBodySize());
                channelFactory.Close();
            }
        }

        [Fact]
        public void Kestrel_MaxRequestBodySize_Not_Decreased_When_Higher_Than_Binding()
        {
            var builder = ServiceHelper.CreateWebHostBuilder<Startup>(_output);
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
                System.ServiceModel.ChannelFactory<IHttpMaxRequestBodySizeService> channelFactory = null;
                channelFactory = new System.ServiceModel.ChannelFactory<IHttpMaxRequestBodySizeService>(ClientHelper.GetBufferedModeBinding(),
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/TestService/basichttp.svc")));
                var channel = channelFactory.CreateChannel();
                Assert.Equal(100_000, channel.GetMaxRequestBodySize());
                channelFactory.Close();
            }
        }

        [WindowsOnlyFact]
#if NET8_0_OR_GREATER
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
        public void HttpSys_MaxRequestBodySize_Increased_When_Lower_Than_Binding()
        {
            var builder = ServiceHelper.CreateHttpSysBuilder<Startup>(_output);
            builder.ConfigureServices(services =>
            {
                services.Configure<HttpSysOptions>(options =>
                {
                    options.MaxRequestBodySize = 10;
                });
            });
            IWebHost host = builder.Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.ChannelFactory<IHttpMaxRequestBodySizeService> channelFactory = null;
                channelFactory = new System.ServiceModel.ChannelFactory<IHttpMaxRequestBodySizeService>(ClientHelper.GetBufferedModeBinding(),
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost/Temporary_Listen_Addresses/CoreWCFTestServices/TestService/basichttp.svc")));
                var channel = channelFactory.CreateChannel();
                Assert.Equal(65536, channel.GetMaxRequestBodySize());
                channelFactory.Close();
            }
        }

        [WindowsOnlyFact]
#if NET8_0_OR_GREATER
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
        public void HttpSys_MaxRequestBodySize_Not_Decreased_When_Higher_Than_Binding()
        {
            var builder = ServiceHelper.CreateHttpSysBuilder<Startup>(_output);
            builder.ConfigureServices(services =>
            {
                services.Configure<HttpSysOptions>(options =>
                {
                    options.MaxRequestBodySize = 100_000;
                });
            });
            IWebHost host = builder.Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.ChannelFactory<IHttpMaxRequestBodySizeService> channelFactory = null;
                channelFactory = new System.ServiceModel.ChannelFactory<IHttpMaxRequestBodySizeService>(ClientHelper.GetBufferedModeBinding(),
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost/Temporary_Listen_Addresses/CoreWCFTestServices/TestService/basichttp.svc")));
                var channel = channelFactory.CreateChannel();
                Assert.Equal(100_000, channel.GetMaxRequestBodySize());
                channelFactory.Close();
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddTransient<HttpMaxRequestBodySizeService>();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<HttpMaxRequestBodySizeService>();
                    builder.AddServiceEndpoint<HttpMaxRequestBodySizeService, IHttpMaxRequestBodySizeService>(new BasicHttpBinding(), "/TestService/basichttp.svc");
                });
            }
        }

        [System.ServiceModel.ServiceContract]
        public interface IHttpMaxRequestBodySizeService
        {
            [System.ServiceModel.OperationContract]
            long GetMaxRequestBodySize();
        }

        [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
        public partial class HttpMaxRequestBodySizeService : IHttpMaxRequestBodySizeService
        {
            public long GetMaxRequestBodySize([Injected] HttpContext context) =>
                context.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize ?? 0;
        }
    }
}
