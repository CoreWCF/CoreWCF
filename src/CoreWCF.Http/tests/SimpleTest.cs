// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class SimpleTest
    {
        private readonly ITestOutputHelper _output;

        public SimpleTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task BasicHttpRequestReplyEchoString()
        {
            string testString = new('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

        [Fact]
        public async Task BasicHttpConfigureServiceHostBaseEchoString()
        {
            string testString = new('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupWithConfiguration>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                Assert.True(StartupWithConfiguration.ConfigureServiceHostValid);
            }
        }

        [Fact]
        public async Task BasicHttpNonGenericConfigureServiceHostBaseEchoString()
        {
            string testString = new('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupWithNonGenericConfiguration>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                Assert.True(StartupWithNonGenericConfiguration.ConfigureServiceHostValid);
            }
        }

        [Fact]
        public async Task BasicHttpConfigureAllServiceHostBaseEchoString()
        {
            string testString = new('a', 3000);

            void Act(System.ServiceModel.BasicHttpBinding httpBinding, Uri uri)
            {
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(uri));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }

            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupWithAllConfiguration>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();

                Act(httpBinding, new Uri("http://localhost:8080/EchoService/basichttp.svc"));

                Assert.True(StartupWithAllConfiguration.ConfigureServiceHostValid);

                Act(httpBinding, new Uri("http://localhost:8080/EchoService2/basichttp.svc"));

                Assert.True(StartupWithAllConfiguration.ConfigureServiceHostValid2);
            }
        }

        [Fact]
        public async Task BasicHttpNonGenericConfigureServiceHostBaseNotAClassThrows()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupWithNonGenericConfigurationWithInterface>(_output).Build();
            using (host)
            {
                var exception = await Assert.ThrowsAsync<ArgumentException>(() => host.StartAsync());
                Assert.Equal("serviceType", exception.ParamName);
            }
        }

        internal class Startup
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

            public void Configure(IApplicationBuilder app)
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

        internal class StartupWithNonGenericConfiguration
        {
            public static bool ConfigureServiceHostValid { get; set; } = false;
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                    builder.ConfigureServiceHostBase(typeof(Services.EchoService), serviceHost =>
                    {
                        ConfigureServiceHostValid = serviceHost.Description.ServiceType == typeof(Services.EchoService);
                    });
                });
            }
        }

        internal class StartupWithAllConfiguration
        {
            public static bool ConfigureServiceHostValid { get; set; } = false;
            public static bool ConfigureServiceHostValid2 { get; set; } = false;
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddService<Services.EchoService2>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/EchoService/basichttp.svc");
                    builder.AddServiceEndpoint<Services.EchoService2, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/EchoService2/basichttp.svc");
                    builder.ConfigureAllServiceHostBase(serviceHost =>
                    {
                        if (serviceHost.Description.ServiceType == typeof(Services.EchoService))
                        {
                            ConfigureServiceHostValid = true;
                        }

                        if (serviceHost.Description.ServiceType == typeof(Services.EchoService2))
                        {
                            ConfigureServiceHostValid2 = true;
                        }
                    });
                });
            }
        }

        internal class StartupWithNonGenericConfigurationWithInterface
        {
            public static bool ConfigureServiceHostValid { get; set; } = false;
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                    builder.ConfigureServiceHostBase(typeof(ServiceContract.IEchoService), serviceHost =>
                    {
                        // Noop
                    });
                });
            }
        }
    }
}

