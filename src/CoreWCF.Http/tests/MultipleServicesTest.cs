// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class MultipleServicesTest
    {
        private readonly ITestOutputHelper _output;

        public MultipleServicesTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task AddMultipleServices()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var echoServiceFactory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/IEchoService.svc")));
                ClientContract.IEchoService echoServiceChannel = echoServiceFactory.CreateChannel();

                var messageEncodingServiceFactory = new System.ServiceModel.ChannelFactory<ClientContract.IMessageEncodingService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/IMessageEncodingServiceFactory.svc")));
                ClientContract.IMessageEncodingService messageEncodingServiceChannel = messageEncodingServiceFactory.CreateChannel();

                // Verify the EchoService echoes the input string
                string toEcho = "hello, world!";
                string result = echoServiceChannel.EchoString(toEcho);
                Assert.NotNull(result);
                Assert.Equal(toEcho, result);

                // Verify the MessageEncodingService echoes the input byte array
                byte[] bytesToEcho = Encoding.ASCII.GetBytes("hello, bytes!");
                byte[] bytesResult = messageEncodingServiceChannel.EchoByteArray(bytesToEcho);
                Assert.NotNull(result);
                Assert.Equal(bytesToEcho, bytesResult);
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new BasicHttpBinding(), "/BasicWcfService/IEchoService.svc");

                    builder.AddService<Services.MessageEncodingService>();
                    builder.AddServiceEndpoint<Services.MessageEncodingService, ServiceContract.IMessageEncodingService>(new BasicHttpBinding(), "/BasicWcfService/IMessageEncodingServiceFactory.svc");
                });
            }
        }
    }
}
