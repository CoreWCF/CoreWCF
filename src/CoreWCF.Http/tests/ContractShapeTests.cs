// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class ContractShapeTests
    {
        private readonly ITestOutputHelper _output;

        public ContractShapeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TwowayUsingParamsKeyword()
        {
            var host = ServiceHelper.CreateWebHostBuilder<ContractShapeParamsServiceStartup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IServiceContract_Params>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/ContractShapeParamsService.svc")));
                var channel = factory.CreateChannel();

                int[] nums = { 0, 1, 5, 25 };
                foreach (var numberOfParams in nums)
                {
                    int[] paramVals = new int[numberOfParams];
                    for (int itemNum = 0; itemNum < numberOfParams; itemNum++)
                    {
                        paramVals[itemNum] = itemNum;
                    }

                    string response = channel.TwoWayParamArray(numberOfParams, paramVals);
                    Assert.Equal($"Service recieved and processed {numberOfParams} args", response);
                }
            }
        }

        [Fact]
        public void MuptiOverloadedMethod()
        {
            var host = ServiceHelper.CreateWebHostBuilder<ContractShapeOverloadsServiceStartup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IServiceContract_Overloads>(httpBinding,
                          new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/ContractShapeOverloadsService.svc")));
                var channel = factory.CreateChannel();

                // Call into the appropriate overload per variation
                string response = channel.TwoWayMethod();
                Assert.Equal("Server Received: Void", response);

                response = channel.TwoWayMethod(12345);
                Assert.Equal("Server Received: 12345", response);

                response = channel.TwoWayMethod("String From Client");
                Assert.Equal("Server Received: String From Client", response);

                var ctToSend = new ClientContract.SM_ComplexType
                {
                    s = "8675309",
                    n = 8675309
                };

                response = channel.TwoWayMethod(ctToSend);
                Assert.Equal("Server Received: 8675309 and 8675309", response);
            }
        }

        internal class ContractShapeOverloadsServiceStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.ContractShapeOverloadsService>();
                    builder.AddServiceEndpoint<Services.ContractShapeOverloadsService, ServiceContract.IServiceContract_Overloads>(new BasicHttpBinding(), "/BasicWcfService/ContractShapeOverloadsService.svc");
                });
            }
        }

        internal class ContractShapeParamsServiceStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.ContractShapeParamsService>();
                    builder.AddServiceEndpoint<Services.ContractShapeParamsService, ServiceContract.IServiceContract_Params>(new BasicHttpBinding(), "/BasicWcfService/ContractShapeParamsService.svc");
                });
            }
        }
    }
}
