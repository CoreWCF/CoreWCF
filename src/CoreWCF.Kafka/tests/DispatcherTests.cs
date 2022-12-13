// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
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

public class DispatcherTests : IntegrationTest
{
    public DispatcherTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [Fact]
    [Trait("Category", "LinuxOnly")]
    public void DispatchOperationTests()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(Output, ConsumerGroup, Topic).Build();
        using (host)
        {
            host.Start();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<DispatcherTestService>();
            testService.CountdownEvent1.Reset(1);
            testService.CountdownEvent2.Reset(1);

            ServiceModel.Channels.KafkaBinding kafkaBinding = new();
            var factory = new System.ServiceModel.ChannelFactory<IDispatcherTestContract>(kafkaBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://localhost:9092/{Topic}")));
            IDispatcherTestContract channel = factory.CreateChannel();

            string name = Guid.NewGuid().ToString();
            channel.Op1(name);
            channel.Op2(name);

            Assert.True(testService.CountdownEvent1.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(testService.CountdownEvent2.Wait(TimeSpan.FromSeconds(10)));

            Assert.Contains(name, testService.Names1);
            Assert.Contains(name, testService.Names2);
        }
    }

    private class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<DispatcherTestService>();
            services.AddServiceModelServices();
            services.AddQueueTransport();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(services =>
            {
                var topicNameAccessor = app.ApplicationServices.GetService<TopicNameAccessor>();
                var consumerGroupAccessor = app.ApplicationServices.GetService<ConsumerGroupAccessor>();
                services.AddService<DispatcherTestService>();
                services.AddServiceEndpoint<DispatcherTestService, IDispatcherTestContract>(new KafkaBinding
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    DeliverySemantics = KafkaDeliverySemantics.AtMostOnce,
                    GroupId = consumerGroupAccessor.Invoke()
                }, $"net.kafka://localhost:9092/{topicNameAccessor.Invoke()}");
            });
        }
    }
}
