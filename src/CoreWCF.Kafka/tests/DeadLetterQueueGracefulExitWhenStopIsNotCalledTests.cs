// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using Contracts;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Kafka.Tests.Helpers;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests;

public class DeadLetterQueueGracefulExitWhenStopIsNotCalledTests : IntegrationTest
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

    public DeadLetterQueueGracefulExitWhenStopIsNotCalledTests(ITestOutputHelper output)
        : base(output, true)
    {

    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaProducerTest()
    {
        StopCountHolder stopCountHolder = new();
        WebApplication webApplication = ServiceHelper.CreateWebApplication(Output, ConsumerGroup, Topic, services =>
        {
            Startup.ConfigureServices(services);
            services.AddSingleton(_ => stopCountHolder);
        }, Startup.Configure);
        await using (webApplication)
        {
            await webApplication.StartAsync();
            var resolver = new DependencyResolverHelper(webApplication.Services);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(1);
            using var producer = new ProducerBuilder<Null, string>(new ProducerConfig
                {
                    BootstrapServers = "localhost:9092",
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

            // we cannot rely on the fact that the message is dispatched to the service
            // because the service is stopped before the message is dispatched.
            // so we wait a bit to be sure the message is consumed.
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        await Task.Delay(TimeSpan.FromSeconds(10));

        Assert.Equal(0, await KafkaEx.GetConsumerLagAsync(Output, ConsumerGroup, Topic));
        Assert.Equal(1, await KafkaEx.GetMessageCountAsync(Output, DeadLetterQueueTopic));
        Assert.Equal(0, stopCountHolder.Value);
    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaClientBindingTest()
    {
        StopCountHolder stopCountHolder = new();
        WebApplication webApplication = ServiceHelper.CreateWebApplication(Output, ConsumerGroup, Topic, services =>
        {
            Startup.ConfigureServices(services);
            services.AddSingleton(_ => stopCountHolder);
        }, Startup.Configure);
        await using (webApplication)
        {
            await webApplication.StartAsync();
            var resolver = new DependencyResolverHelper(webApplication.Services);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(1);

            ServiceModel.Channels.KafkaBinding kafkaBinding = new();
            var factory = new System.ServiceModel.ChannelFactory<ITestContract>(kafkaBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://localhost:9092/{Topic}")));
            ITestContract channel = factory.CreateChannel();

            // send a first a message so the consumerGroup has consumed at least one message of the topic partition.
            string name = Guid.NewGuid().ToString();
            await channel.CreateAsync(name);
            channel.Throw(name);

            // we cannot rely on the fact that the message is dispatched to the service
            // because the service is stopped before the message is dispatched.
            // so we wait a bit to be sure the message is consumed.
            await Task.Delay(TimeSpan.FromSeconds(10));
        }

        await Task.Delay(TimeSpan.FromSeconds(10));

        Assert.Equal(0, await KafkaEx.GetConsumerLagAsync(Output, ConsumerGroup, Topic));
        Assert.Equal(1, await KafkaEx.GetMessageCountAsync(Output, DeadLetterQueueTopic));
        Assert.Equal(0, stopCountHolder.Value);
    }

    private static class Startup
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            services.Configure<HostOptions>(options =>
            {
                options.ShutdownTimeout = TimeSpan.FromSeconds(1);
            });
            services.AddHostedService<FirstHostedService>();
            services.AddSingleton<TestService>();
            services.AddServiceModelServices();
            services.AddQueueTransport();
            services.AddHostedService<ThirdHostedService>();
        }

        public static void Configure(WebApplication app)
        {
            app.UseServiceModel(services =>
            {
                var topicNameAccessor = app.Services.GetService<TopicNameAccessor>();
                var deadLetterQueueTopicNameAccessor = app.Services.GetService<DeadLetterQueueTopicNameAccessor>();
                var consumerGroupAccessor = app.Services.GetService<ConsumerGroupAccessor>();
                services.AddService<TestService>();
                services.AddServiceEndpoint<TestService, ITestContract>(new KafkaBinding
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    DeliverySemantics = KafkaDeliverySemantics.AtMostOnce,
                    ErrorHandlingStrategy = KafkaErrorHandlingStrategy.DeadLetterQueue,
                    DeadLetterQueueTopic = deadLetterQueueTopicNameAccessor.Invoke(),
                    GroupId = consumerGroupAccessor.Invoke()
                }, $"net.kafka://localhost:9092/{topicNameAccessor.Invoke()}");
            });
        }
    }

    private class StopCountHolder
    {
        public int Value { get; set; } = 0;
    }

    private class FirstHostedService : IHostedService
    {
        private readonly StopCountHolder _stopCountHolder;

        public FirstHostedService(StopCountHolder stopCountHolder)
        {
            _stopCountHolder = stopCountHolder;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _stopCountHolder.Value++;
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    private class ThirdHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}
