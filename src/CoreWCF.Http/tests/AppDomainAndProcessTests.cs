using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{

    public class AppDomainAndProcessTests
    {
        private ITestOutputHelper _output;

        public AppDomainAndProcessTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SingleDomainTests()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                string testString = "Hello World!";
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IRequestReplyService>(httpBinding,
                   new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/AppDomainAndProcessTests.svc")));
                var channel = factory.CreateChannel();
                string resultString = channel.Echo(testString);
                
                Assert.Equal(testString, resultString);
            }
        }

        [Fact]
        public void CrossDomainTests()
        {
            //ServiceHost don't support
        }

        [Fact]
        public void CrossProcessSelfHostTests()
        {
            //ServiceHost don't support
        }
    }

    internal class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(Microsoft.AspNetCore.Builder.IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<Services.RequestReplyService>();
                builder.AddServiceEndpoint<Services.RequestReplyService, ServiceContract.IRequestReplyService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/AppDomainAndProcessTests.svc");
            });
        }
    }
}
