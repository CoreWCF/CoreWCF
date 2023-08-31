// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Contracts;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.MSMQ.Tests.Helpers;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.MSMQ.Tests
{
    public class IntegrationTests
    {
        private readonly ITestOutputHelper _output;
        public const string QueueName = "wcfQueue";
        public const string QueueNameDeadLetter = "wcfDeadLetter";

        public IntegrationTests(ITestOutputHelper output)
        {
            _output = output;
            MessageQueueHelper.SetRequirements(QueueName);
            MessageQueueHelper.SetRequirements(QueueNameDeadLetter);
            MessageQueueHelper.Purge(QueueName);
            MessageQueueHelper.Purge(QueueNameDeadLetter);
        }

        [Fact(Skip = "Need msmq")]
        public void ReceiveMessage_ServiceCall_Success()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                MessageQueueHelper.SendMessageInQueue(QueueName);

                var resolver = new DependencyResolverHelper(host);
                var testService = resolver.GetService<TestService>();
                Assert.True(testService.ManualResetEvent.Wait(System.TimeSpan.FromSeconds(5)));
            }
        }

        [Fact(Skip = "Need msmq")]
        public async Task ReceiveMessage_ServiceCall_Fail()
        {
            MessageQueueHelper.PurgeDeadLetter();
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                MessageQueueHelper.SendBadMessageInQueue(QueueName);

                bool result = await MessageQueueHelper.WaitMessageInDeadLetter();
                Assert.True(result);
            }
        }

        [Fact(Skip = "implement with failed message")]
        public async Task ReceiveMessage_ServiceCall_Fail_ShouldSendCustomDeadLetter()
        {
            MessageQueueHelper.PurgeDeadLetter();
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup2>(_output).Build();
            using (host)
            {
                host.Start();
                MessageQueueHelper.SendEmptyMessageInQueue(QueueName);

                bool result = await MessageQueueHelper.WaitMessageInQueue(QueueNameDeadLetter);
                Assert.True(result);
            }
        }
    }

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<TestService>();
            services.AddServiceModelServices();
            services.AddQueueTransport();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(services =>
            {
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(new NetMsmqBinding(),
                    $"net.msmq://localhost/private/{IntegrationTests.QueueName}");
            });
        }
    }

    public class Startup2
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<TestService>();
            services.AddServiceModelServices();
            services.AddQueueTransport();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(services =>
            {
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(
                    new NetMsmqBinding
                    {
                        DeadLetterQueue = DeadLetterQueue.Custom,
                        CustomDeadLetterQueue =
                            new System.Uri($"net.msmq://localhost/private/{IntegrationTests.QueueNameDeadLetter}")
                    },
                    $"net.msmq://localhost/private/{IntegrationTests.QueueName}");
            });
        }
    }
}
