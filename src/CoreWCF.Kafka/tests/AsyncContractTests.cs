// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Confluent.Kafka;
using Contracts;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Kafka.Tests.Helpers;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests;

public class AsyncContractTests : IntegrationTest
{
    public AsyncContractTests(ITestOutputHelper output) : base(output)
    {
    }

    [Fact]
    [Trait("Category", "LinuxOnly")]
    public async Task AsyncOperationContractTest()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(Output, ConsumerGroup, Topic).Build();
        using (host)
        {
            await host.StartAsync();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(1);

            ServiceModel.Channels.KafkaBinding kafkaBinding = new();
            var factory = new System.ServiceModel.ChannelFactory<ITestContract>(kafkaBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://localhost:9092/{Topic}")));

            ITestContract channel = factory.CreateChannel();
            string name = Guid.NewGuid().ToString();

            await channel.CreateAsync(name);

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Contains(name, testService.Names);
        }
    }

    private class Startup
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
                var topicNameAccessor = app.ApplicationServices.GetService<TopicNameAccessor>();
                var consumerGroupAccessor = app.ApplicationServices.GetService<ConsumerGroupAccessor>();
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(new KafkaBinding
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    DeliverySemantics = KafkaDeliverySemantics.AtMostOnce,
                    GroupId = consumerGroupAccessor.Invoke()
                }, $"net.kafka://localhost:9092/{topicNameAccessor.Invoke()}");
            });
        }
    }
}
