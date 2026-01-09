// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using ClientContract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class MCWrappedMultiNSTests
    {
        private readonly ITestOutputHelper _output;

        public MCWrappedMultiNSTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void MulDataContractsInDiffNS()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IMCWrappedMultiNS>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/MCWrappedMultiNSService.svc")));
                IMCWrappedMultiNS channel = factory.CreateChannel();

                MC2MultiNS RetMC = channel.M(new MCMultiNS());
                Assert.NotNull(RetMC);
            }
        }

        [Fact]
        public void MulDataContractsInDiffNS2()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup2>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IMCWrappedMultiNS>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/MCWrappedMultiNSService.svc")));
                IMCWrappedMultiNS channel = factory.CreateChannel();

                MC2MultiNS RetMC = channel.M(new MCMultiNS());
                Assert.NotNull(RetMC);
            }
        }

        [Fact]
        public void MulDataContractsInDiffNS3()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup3>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IMCWrappedMultiNS>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/MCWrappedMultiNSService.svc")));
                IMCWrappedMultiNS channel = factory.CreateChannel();

                MC2MultiNS RetMC = channel.M(new MCMultiNS());
                Assert.NotNull(RetMC);
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
                    builder.AddService<Services.ServerWrappedMultipleNSService>();
                    builder.AddServiceEndpoint<Services.ServerWrappedMultipleNSService, ServiceContract.IMCWrappedMultiNS>(new BasicHttpBinding(), "/BasicWcfService/MCWrappedMultiNSService.svc");
                });
            }
        }

        internal class Startup2
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.ServerWrappedMultipleNSService2>();
                    builder.AddServiceEndpoint<Services.ServerWrappedMultipleNSService2, ServiceContract.IMCWrappedMultiNS2>(new BasicHttpBinding(), "/BasicWcfService/MCWrappedMultiNSService.svc");
                });
            }
        }

        internal class Startup3
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.ServerWrappedMultipleNSService3>();
                    builder.AddServiceEndpoint<Services.ServerWrappedMultipleNSService3, ServiceContract.IMCWrappedMultiNS3>(new BasicHttpBinding(), "/BasicWcfService/MCWrappedMultiNSService.svc");
                });
            }
        }
    }
}
