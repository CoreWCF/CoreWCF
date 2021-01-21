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
    public class TestFaultContractName1Tests
    {
        private ITestOutputHelper _output;

        public TestFaultContractName1Tests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void FaultContractsVaryName()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestFaultContractName1>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/TestFaultContractName1.svc")));
                var channel = factory.CreateChannel();

                string faultToThrow = "Test fault thrown from a service";

                //variation method1
                try
                {
                    string s = channel.Method1("");
                }
                catch (Exception e)
                {
                    Assert.NotNull(e);
                    Assert.IsType<System.ServiceModel.FaultException<string>>(e);
                    var ex = (System.ServiceModel.FaultException<string>)e;
                    Assert.Equal(faultToThrow, ex.Detail.ToString());
                }
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
                    builder.AddService<Services.FaultContract_859456_Service1>();
                    builder.AddServiceEndpoint<Services.FaultContract_859456_Service1, ServiceContract.ITestFaultContractName1>(new BasicHttpBinding(), "/BasicWcfService/TestFaultContractName1.svc");
                });
            }
        }
    }
}
