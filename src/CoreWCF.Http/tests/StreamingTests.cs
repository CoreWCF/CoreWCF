using System;
using System.IO;
using ClientContract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class StreamingTests
    {
        private ITestOutputHelper _output;
        public StreamingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void StreamingIntermediary()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModHttpBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IForward>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfServiceForward/StreamingTest.svc")));
                IForward forward = factory.CreateChannel();

                long num = 1000000000;
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    num /= 1000L;
                }
                RequestStream input = new RequestStream(num);
                Stream stream = forward.Forward(input);
                byte[] buffer = new byte[2000];
                int num2 = 0;
                int num3;
                while ((num3 = stream.Read(buffer, 0, 2000)) != 0)
                {
                    num2 = num3 + num2;
                }
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
                builder.AddService<Services.EchoForwardService>();
                builder.AddServiceEndpoint<Services.EchoForwardService, ServiceContract.IEcho>(ServiceHelper.GetBinding(), "/BasicWcfServiceEcho/StreamingTest1.svc");
                builder.AddServiceEndpoint<Services.EchoForwardService, ServiceContract.IForward>(ServiceHelper.GetBufferedModHttpBinding(), "/BasicWcfServiceForward/StreamingTest.svc");
            });
        }
    }
}
