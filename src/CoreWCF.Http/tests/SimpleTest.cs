using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class SimpleTest
    {
        private ITestOutputHelper _output;

        public SimpleTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicHttpRequestReplyEchoString()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                var channel = factory.CreateChannel();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

        [Fact]
        public void BasicHttpConfigureSericeHostBaseEchoString()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<StartupWithConfiguration>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                var channel = factory.CreateChannel();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                Assert.True(StartupWithConfiguration.ConfigureServiceHostValid);
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }

        internal class StartupWithConfiguration
        {
            public static bool ConfigureServiceHostValid { get; set; } = false;
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                    builder.ConfigureServiceHostBase<Services.EchoService>(serviceHost =>
                    {
                        ConfigureServiceHostValid = serviceHost.Description.ServiceType == typeof(Services.EchoService);
                    });
                });
            }
        }
    }
}