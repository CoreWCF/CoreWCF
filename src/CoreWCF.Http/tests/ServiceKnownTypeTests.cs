using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class ServiceKnownTypeTests
    {
        private ITestOutputHelper _output;

        public ServiceKnownTypeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ServiceKnownTypeSerializes()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup<ServiceContract.IServiceKnownTypeTest>>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.ChannelFactory<ClientContract.IServiceKnownTypeTest> channelFactory = null;
                channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.IServiceKnownTypeTest>(ClientHelper.GetBufferedModHttp1Binding(),
                      new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/ServiceKnownType/HttpEndpoint.svc")));
                var channel = channelFactory.CreateChannel();
                var request = new ClientContract.HelloRequest { Name = "Bill Gates" };
                var responseBase = channel.SayHello(request);
                Assert.NotNull(responseBase);
                Assert.IsType<ClientContract.HelloReply>(responseBase);
                var response = responseBase as ClientContract.HelloReply;
                Assert.Equal("Hello " + request.Name, response.Message);
                ((System.ServiceModel.IClientChannel)channel).Close();
                channelFactory.Close();
            }
        }

        [Fact]
        public void ServiceKnownTypeCompatibilitySerializes()
        {
            // This variant uses the System.ServiceModel namespaced attributes on the service side
            var host = ServiceHelper.CreateWebHostBuilder<Startup<ClientContract.IServiceKnownTypeTest>>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.ChannelFactory<ClientContract.IServiceKnownTypeTest> channelFactory = null;
                channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.IServiceKnownTypeTest>(ClientHelper.GetBufferedModHttp1Binding(),
                      new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/ServiceKnownType/HttpEndpoint.svc")));
                var channel = channelFactory.CreateChannel();
                var request = new ClientContract.HelloRequest { Name = "Bill Gates" };
                var responseBase = channel.SayHello(request);
                Assert.NotNull(responseBase);
                Assert.IsType<ClientContract.HelloReply>(responseBase);
                var response = responseBase as ClientContract.HelloReply;
                Assert.Equal("Hello " + request.Name, response.Message);
                ((System.ServiceModel.IClientChannel)channel).Close();
                channelFactory.Close();
            }
        }

        internal class Startup<TContract>
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.ServiceKnownTypeService>();
                    builder.AddServiceEndpoint<Services.ServiceKnownTypeService, TContract>(ServiceHelper.GetBufferedModHttp1Binding(), "/ServiceKnownType/HttpEndpoint.svc");
                });
            }
        }
    }
}
