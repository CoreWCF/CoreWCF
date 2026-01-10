// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Confluent.Kafka;
using Contracts;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Kafka.Tests.Helpers;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests;

public class RegexSubscriptionTests : MultipleTopicsIntegrationTest
{
    private const string MessageTemplate =
        @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:a=""http://www.w3.org/2005/08/addressing"">"
        + @"<s:Header><a:Action s:mustUnderstand=""1"">http://tempuri.org/ITestContract/Create</a:Action></s:Header>"
        + @"<s:Body><Create xmlns=""http://tempuri.org/""><name>{0}</name></Create></s:Body>"
        + @"</s:Envelope>";

    public RegexSubscriptionTests(ITestOutputHelper output, KafkaContainerFixture containerFixture)
        : base(output, containerFixture)
    {

    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaProducerTest()
    {
        IHost host = ServiceHelper.CreateHost<Startup>(Output, ConsumerGroup, TopicRegex);
        using (host)
        {
            await host.StartAsync();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(Topics.Count);
            using var producer = new ProducerBuilder<Null, string>(new ProducerConfig
                {
                    BootstrapServers = KafkaEx.GetBootstrapServers(),
                    Acks = Acks.All
                })
                .SetKeySerializer(Serializers.Null)
                .SetValueSerializer(Serializers.Utf8)
                .Build();

            string name = Guid.NewGuid().ToString();
            string value = string.Format(MessageTemplate, name);
            foreach (string topic in Topics)
            {
                var result = await producer.ProduceAsync(topic, new Message<Null, string> { Value = value });
                Assert.True(result.Status == PersistenceStatus.Persisted);
            }

            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(3)));

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Contains(name, testService.Names);
        }

        foreach (string topic in Topics)
        {
            await AssertEx.RetryAsync(() => Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, topic)));
        }
    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaClientBindingTest()
    {
        IHost host = ServiceHelper.CreateHost<Startup>(Output, ConsumerGroup, TopicRegex);
        using (host)
        {
            await host.StartAsync();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(Topics.Count);

            List<string> names = new();
            foreach (string topic in Topics)
            {
                ServiceModel.Channels.KafkaBinding kafkaBinding = new();
                var factory = new System.ServiceModel.ChannelFactory<ITestContract>(kafkaBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://{KafkaEx.GetBootstrapServers()}/{topic}")));
                ITestContract channel = factory.CreateChannel();

                string name = Guid.NewGuid().ToString();
                await channel.CreateAsync(name);
                names.Add(name);
            }

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            foreach (string name in names)
            {
                Assert.Contains(name, testService.Names);
            }
        }

        foreach (string topic in Topics)
        {
            await AssertEx.RetryAsync(() =>Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, topic)));
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

        public void Configure(IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IHostingEnvironment env)
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
                }, $"net.kafka://{KafkaEx.GetBootstrapServers()}/{topicNameAccessor.Invoke()}");
            });
        }
    }
}
