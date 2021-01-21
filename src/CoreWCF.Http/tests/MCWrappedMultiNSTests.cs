// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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
        private ITestOutputHelper _output;

        public MCWrappedMultiNSTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void MulDataContractsInDiffNS()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IMCWrappedMultiNS>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/MCWrappedMultiNSService.svc")));
                var channel = factory.CreateChannel();

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

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.ServerWrappedMultipleNSService>();
                    builder.AddServiceEndpoint<Services.ServerWrappedMultipleNSService, ServiceContract.IMCWrappedMultiNS>(new BasicHttpBinding(), "/BasicWcfService/MCWrappedMultiNSService.svc");
                });
            }
        }
    }
}
