using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
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

        [Theory]
        [InlineData(typeof(Startup<Services.ServiceKnownTypeService, ClientContract.IServiceKnownTypeTest>))]
        [InlineData(typeof(Startup<Services.ServiceKnownTypeService, ServiceContract.IServiceKnownTypeWithType>))]
        [InlineData(typeof(Startup<Services.ServiceKnownTypeService, ServiceContract.IServiceKnownTypeWithDeclaredTypeAndMethodName>))]
        [InlineData(typeof(Startup<Services.ServiceKnownTypeWithAttribute, ServiceContract.IServiceKnownTypeBase>))]
        public void ServiceKnownTypeSucceeds(Type startupType)
        {
            var host = ServiceHelper.CreateWebHostBuilder(startupType, _output).Build();
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

        internal class Startup<TService, TContract> where TService : class
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<TService>();
                    builder.AddServiceEndpoint<TService, TContract>(ServiceHelper.GetBufferedModHttp1Binding(), "/ServiceKnownType/HttpEndpoint.svc");
                });
            }
        }
    }
}
