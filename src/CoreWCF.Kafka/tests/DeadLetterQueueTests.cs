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

public class DeadLetterQueueTests : IntegrationTest
{
    private const string MessageTemplate =
        @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:a=""http://www.w3.org/2005/08/addressing"">"
        + @"<s:Header><a:Action s:mustUnderstand=""1"">http://tempuri.org/ITestContract/Create</a:Action></s:Header>"
        + @"<s:Body><Create xmlns=""http://tempuri.org/""><name>{0}</name></Create></s:Body>"
        + @"</s:Envelope>";

    private const string ThrowMessageTemplate =
        @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:a=""http://www.w3.org/2005/08/addressing"">"
        + @"<s:Header><a:Action s:mustUnderstand=""1"">http://tempuri.org/ITestContract/Throw</a:Action></s:Header>"
        + @"<s:Body><Create xmlns=""http://tempuri.org/""><name>{0}</name></Throw></s:Body>"
        + @"</s:Envelope>";

    public DeadLetterQueueTests(ITestOutputHelper output, KafkaContainerFixture containerFixture)
        : base(output, containerFixture, true)
    {

    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaProducerTest()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(Output, ConsumerGroup, Topic).Build();
        using (host)
        {
            await host.StartAsync();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(1);
            using var producer = new ProducerBuilder<Null, string>(new ProducerConfig
                {
                    BootstrapServers = KafkaEx.GetBootstrapServers(),
                    Acks = Acks.All
                })
                .SetKeySerializer(Serializers.Null)
                .SetValueSerializer(Serializers.Utf8)
                .Build();

            // send a first a message so the consumerGroup has consumed at least one message of the topic partition.
            var result = await producer.ProduceAsync(Topic, new Message<Null, string> { Value = string.Format(MessageTemplate, Guid.NewGuid().ToString()) });
            Assert.True(result.Status == PersistenceStatus.Persisted);

            // send a message that throw
            result = await producer.ProduceAsync(Topic, new Message<Null, string> { Value = string.Format(ThrowMessageTemplate, Guid.NewGuid()) });
            Assert.True(result.Status == PersistenceStatus.Persisted);

            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(3)));
            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Single(testService.Names);
        }

        await AssertEx.RetryAsync(() => Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic)));
        await AssertEx.RetryAsync(() => Assert.Equal(1, KafkaEx.GetMessageCount(Output, DeadLetterQueueTopic)));
    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaClientBindingTest()
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
                new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://{KafkaEx.GetBootstrapServers()}/{Topic}")));
            ITestContract channel = factory.CreateChannel();

            // send a first a message so the consumerGroup has consumed at least one message of the topic partition.
            string name = Guid.NewGuid().ToString();
            await channel.CreateAsync(name);
            channel.Throw(name);

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Contains(name, testService.Names);
        }

        await AssertEx.RetryAsync(() => Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic)));
        await AssertEx.RetryAsync(() => Assert.Equal(1, KafkaEx.GetMessageCount(Output, DeadLetterQueueTopic)));
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
                var deadLetterQueueTopicNameAccessor = app.ApplicationServices.GetService<DeadLetterQueueTopicNameAccessor>();
                var consumerGroupAccessor = app.ApplicationServices.GetService<ConsumerGroupAccessor>();
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(new KafkaBinding
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    DeliverySemantics = KafkaDeliverySemantics.AtMostOnce,
                    ErrorHandlingStrategy = KafkaErrorHandlingStrategy.DeadLetterQueue,
                    DeadLetterQueueTopic = deadLetterQueueTopicNameAccessor.Invoke(),
                    GroupId = consumerGroupAccessor.Invoke()
                }, $"net.kafka://{KafkaEx.GetBootstrapServers()}/{topicNameAccessor.Invoke()}");
            });
        }
    }
}
