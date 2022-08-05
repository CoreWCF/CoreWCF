// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Contracts;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Queue;
using CoreWCF.RabbitMQ.Tests.Fakes;
using CoreWCF.RabbitMQ.Tests.Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
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

        [Fact(Skip ="Need rabbitmq")]
        public async Task ReceiveMessage_ServiceCall_Success()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                MessageQueueHelper.SendMessageInQueue();
                var resolver = new DependencyResolverHelper(host);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (true)
                {
                    var testService = resolver.GetService<Interceptor>();

                    if (string.IsNullOrEmpty(testService.Name))
                    {
                        if (watch.Elapsed.TotalSeconds > 5)
                            Assert.False(true, "Message not received");

                        await Task.Delay(100);
                    }
                    else
                    {
                        Assert.Equal("TestMessage", testService.Name);
                        break;
                    }
                }
            }
        }

        [Fact(Skip = "Need rabbitmq")]
        public async Task ReceiveMessage()
        {
            var handler = new TestConnectionHandler();
            var factory = new RabbitMqTransportFactory(new NullLoggerFactory(), handler);
            var settings = new QueueSettings { QueueName = QueueName };
            var transport = factory.Create(settings);
            _ = transport.StartAsync();
            MessageQueueHelper.SendMessageInQueue();
            await Task.Delay(1000);
            await transport.StopAsync();
            Assert.Equal(1, handler.CallCount);
        }
    }

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<Interceptor>();
            services.AddScoped<TestService>();
            services.AddServiceModelServices();
            services.AddServiceModelQueue(x =>
                x.Queues.Add(new QueueSettings { QueueName = IntegrationTests.QueueName }));
            services.AddServiceModelRabbitMqSupport();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(services =>
            {
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(new RabbitMqBinding(),
                    $"soap.amqp://{IntegrationTests.QueueName}");
            });
        }
    }
}
