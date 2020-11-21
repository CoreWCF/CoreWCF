using CoreWCF.Configuration;
using ClientContract;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using CoreWCF;
using System.ServiceModel.Channels;

namespace BasicHttp
{
   public class ServiceWithMessageBodyAndHeaderTest
    {
        private ITestOutputHelper _output;

        public ServiceWithMessageBodyAndHeaderTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicScenarioServiceMessageBody()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IServiceWithMessageBodyAndHeader>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/Service.svc")));
                var channel = factory.CreateChannel();

             
                CoreEchoMessageResponse result = channel.EchoWithMessageContract (new CoreEchoMessageRequest() { Text = "Message Hello", APIKey = "DEVKEYTOTEST" });
                Assert.NotNull(result);
                ((IChannel)channel).Close();
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
                    builder.AddService<Services.ServiceWithMessageBodyAndHeader>();
                    builder.AddServiceEndpoint<Services.ServiceWithMessageBodyAndHeader, Services.IServiceWithMessageBodyAndHeader>(new BasicHttpBinding(), "/BasicWcfService/Service.svc");
                });
            }
        }
    }
}
