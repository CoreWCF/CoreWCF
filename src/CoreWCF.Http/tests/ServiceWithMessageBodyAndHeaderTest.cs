// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using ClientContract;
using CoreWCF;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class ServiceWithMessageBodyAndHeaderTest
    {
        private readonly ITestOutputHelper _output;

        public ServiceWithMessageBodyAndHeaderTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicScenarioServiceMessageBody()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IServiceWithMessageBodyAndHeader>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/Service.svc")));
                IServiceWithMessageBodyAndHeader channel = factory.CreateChannel();

                var request = new CoreEchoMessageRequest() { Text = "Message Hello", APIKey = "DEVKEYTOTEST", HeaderArrayValues = new[] { "One", "Two", "Three" } };
                CoreEchoMessageResponse result = channel.EchoWithMessageContract(request);
                Assert.NotNull(result);
                Assert.Equal("Saying Hello " + request.Text, result.SayHello);
                Assert.Equal("Saying Hi " + request.Text, result.SayHi);
                Assert.Equal(request.HeaderArrayValues.Length, result.HeaderArrayValues.Length);
                Assert.Equal(request.HeaderArrayValues, result.HeaderArrayValues);
                ((IChannel)channel).Close();
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
                    builder.AddService<Services.ServiceWithMessageBodyAndHeader>();
                    builder.AddServiceEndpoint<Services.ServiceWithMessageBodyAndHeader, Services.IServiceWithMessageBodyAndHeader>(new BasicHttpBinding(), "/BasicWcfService/Service.svc");
                });
            }
        }
    }
}
