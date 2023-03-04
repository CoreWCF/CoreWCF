// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
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
    public class OperationFaultContractInfoAwareServiceTests
    {
        private readonly ITestOutputHelper _output;

        public OperationFaultContractInfoAwareServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task BasicScenario()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ICalculatorService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/Service.svc")));
                ICalculatorService channel = factory.CreateChannel();

                var result = channel.Divide(2, 1);

                var o = host.Services.GetService<Services.OperationFaultContractInfoAwareServiceBehavior>();

                Assert.Contains(o.FaultContractInfos, x => x.Detail.Name == nameof(MathFault));
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddSingleton(new Services.OperationFaultContractInfoAwareServiceBehavior());
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.ConfigureServiceHostBase<Services.OperationFaultContractInfoAwareCalculatorService>(service =>
                    {
                        var behavior = app.ApplicationServices.GetService<Services.OperationFaultContractInfoAwareServiceBehavior>();
                        service.Description.Behaviors.Add(behavior);
                    });
                    builder.AddService<Services.OperationFaultContractInfoAwareCalculatorService>();
                    builder.AddServiceEndpoint<Services.OperationFaultContractInfoAwareCalculatorService, ServiceContract.ICalculatorService>(new BasicHttpBinding(), "/BasicWcfService/Service.svc");
                });
            }
        }
    }
}
