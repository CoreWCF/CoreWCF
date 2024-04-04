// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class MultipleEndpointsTest
    {
        private readonly ITestOutputHelper _output;

        public MultipleEndpointsTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task AddMultipleEndpoints_OneEndpointPathFullyContainsAnother()
        {
            using IWebHost host = ServiceHelper.CreateWebHostBuilder(_output)
                .ConfigureServices(s => s.AddServiceModelServices())
                .Configure(app =>
                {
                    app.UseServiceModel(builder =>
                    {
                        builder.AddService<EndpointPrinterService>();
                        builder.AddServiceEndpoint<EndpointPrinterService, ServiceContract.IPrinterService>(new BasicHttpBinding(), "/echo");
                        builder.AddServiceEndpoint<EndpointPrinterService, ServiceContract.IPrinterService>(new BasicHttpBinding(), "/echo/xyz");
                    });
                }).Build();

            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();

            var serviceFactory = new System.ServiceModel.ChannelFactory<ClientContract.IPrinterService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/echo")));
            ClientContract.IPrinterService serviceChannel = serviceFactory.CreateChannel();

            var xyzServiceFactory = new System.ServiceModel.ChannelFactory<ClientContract.IPrinterService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/echo/xyz")));
            ClientContract.IPrinterService xyzServiceChannel = xyzServiceFactory.CreateChannel();


            Assert.Equal("/echo", await serviceChannel.PrintAsync());
            Assert.Equal("/echo/xyz", await xyzServiceChannel.PrintAsync());
        }

        public class EndpointPrinterService : ServiceContract.IPrinterService
        {
            public Task<string> PrintAsync() => Task.FromResult(OperationContext.Current.EndpointDispatcher.EndpointAddress.Uri.AbsolutePath);
        }
    }    
}
