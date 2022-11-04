// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Contracts;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common.Configuration;
using CoreWCF.RabbitMQ.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.RabbitMQ.Tests
{
    public class IntegrationTests
    {
        private readonly ITestOutputHelper _output;
        public const string QueueName = "wcfQueue";

        public IntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ReceiveMessage_ServiceCall_Success()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                await Task.Delay(3000);
                MessageQueueHelper.SendMessageInQueue();
                var resolver = new DependencyResolverHelper(host);
                var testService = resolver.GetService<TestService>();
                Assert.True(testService.ManualResetEvent.Wait(System.TimeSpan.FromSeconds(5)));
            }
        }

        
        [Fact]
        public void ReceiveMessage()
        {
            MessageQueueHelper.SendMessageInQueue();
        }
    }

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<TestService>();
            services.AddServiceModelServices();
            services.AddQueueTransport();
            services.AddServiceModelRabbitMqSupport();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(services =>
            {
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(new RabbitMqBinding(),
                    $"soap.amqp://localhost:5672/{IntegrationTests.QueueName}");
            });
        }
    }
}
