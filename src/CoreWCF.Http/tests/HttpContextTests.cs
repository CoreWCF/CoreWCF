// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace DependencyInjection
{
    public partial class HttpContextTests
    {
        private const string input = "ABC";

        [ServiceContract]
        [System.ServiceModel.ServiceContract]
        internal interface ISimpleService
        {
            [OperationContract]
            [System.ServiceModel.OperationContract]
            string Echo(string echo);
        }

        internal partial class AssertSameHttpContextInstance : ISimpleService
        {
            public string Echo(string echo)
            {
                HttpContext httpContext = (OperationContext.Current.RequestContext.RequestMessage.Properties.TryGetValue("Microsoft.AspNetCore.Http.HttpContext", out var @object)
                    && @object is HttpContext context)
                    ? context
                    : null;
                IHttpContextAccessor httpContextAccessor = OperationContext.Current.InstanceContext.Extensions.Find<IServiceProvider>().GetService<IHttpContextAccessor>();
                Assert.Same(httpContext, httpContextAccessor.HttpContext);
                return echo;
            }
        }

        [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
        internal class PerCallSimpleService : AssertSameHttpContextInstance { }

        [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
        internal class PerSessionSimpleService : AssertSameHttpContextInstance { }

        [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
        internal class SingleSimpleService : AssertSameHttpContextInstance { }

        internal class Startup<TService> where TService : class
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
#if NETCOREAPP3_1_OR_GREATER
                services.AddHttpContextAccessor();
#else
                services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
#endif
                services.AddTransient<TService>();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<TService>();
                    builder.AddServiceEndpoint<TService, ISimpleService>(new BasicHttpBinding(), "/BasicWcfService/Service.svc");
                });
            }
        }

        private readonly ITestOutputHelper _output;

        public HttpContextTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void PerCall()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup<PerCallSimpleService>>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ISimpleService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/Service.svc")));
                ISimpleService channel = factory.CreateChannel();
                var result = channel.Echo(input);
            }
        }

        [Fact]
        public void PerSession()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup<PerSessionSimpleService>>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ISimpleService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/Service.svc")));
                ISimpleService channel = factory.CreateChannel();
                var result = channel.Echo(input);
            }
        }

        [Fact]
        public void Single()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup<SingleSimpleService>>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ISimpleService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/Service.svc")));
                ISimpleService channel = factory.CreateChannel();
                var result = channel.Echo(input);
            }
        }
    }
}
