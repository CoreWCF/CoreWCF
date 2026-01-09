// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
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
using KafkaBinding = CoreWCF.Channels.KafkaBinding;
using KafkaMessageProperty = CoreWCF.Channels.KafkaMessageProperty;

namespace CoreWCF.Kafka.Tests;

public class InjectedKafkaMessagePropertiesTests : IntegrationTest
{
    private const string MessageTemplate =
        @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:a=""http://www.w3.org/2005/08/addressing"">"
        + @"<s:Header><a:Action s:mustUnderstand=""1"">http://tempuri.org/ITestContract/StoreInjectedKafkaMessageProperty</a:Action></s:Header>"
        + @"<s:Body><StoreInjectedKafkaMessageProperty xmlns=""http://tempuri.org/""></StoreInjectedKafkaMessageProperty></s:Body>"
        + @"</s:Envelope>";

    public InjectedKafkaMessagePropertiesTests(ITestOutputHelper output, KafkaContainerFixture containerFixture)
        : base(output, containerFixture)
    {

    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaProducerTest()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(Output, ConsumerGroup, Topic).Build();
        using (host)
        {
            await host.StartAsync();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(1);
            using var producer = new ProducerBuilder<string, string>(new ProducerConfig
                {
                    BootstrapServers = KafkaEx.GetBootstrapServers(),
                    Acks = Acks.All
                })
                .SetKeySerializer(Serializers.Utf8)
                .SetValueSerializer(Serializers.Utf8)
                .Build();

            Headers headers = new();
            headers.Add("header1", Encoding.UTF8.GetBytes("header1Value"));
            var result = await producer.ProduceAsync(Topic, new Message<string, string>
            {
                Key = "key",
                Value = MessageTemplate,
                Headers = headers
            });

            Assert.True(result.Status == PersistenceStatus.Persisted);
            Assert.Equal(0, producer.Flush(TimeSpan.FromSeconds(3)));

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            KafkaMessageProperty property = testService.KafkaMessageProperty;
            Assert.NotNull(property);
            Assert.Contains(property.Headers, x => x.Key == "header1" && Encoding.UTF8.GetString(x.Value.Span) == "header1Value");
            Assert.Equal(Encoding.UTF8.GetBytes("key").AsMemory(), property.PartitionKey);
            Assert.Equal(Topic, property.Topic);
        }

        await AssertEx.RetryAsync(() => Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic)));
    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaClientBindingTest()
    {
        IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(Output, ConsumerGroup, Topic).Build();
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

            using (var scope = new System.ServiceModel.OperationContextScope((System.ServiceModel.IContextChannel)channel))
            {
                ServiceModel.Channels.KafkaMessageProperty outgoingProperty = new();
                outgoingProperty.Headers.Add(new ServiceModel.Channels.KafkaMessageHeader("header1", Encoding.UTF8.GetBytes("header1Value")));
                outgoingProperty.PartitionKey = Encoding.UTF8.GetBytes("key");
                System.ServiceModel.OperationContext.Current.OutgoingMessageProperties[ServiceModel.Channels.KafkaMessageProperty.Name] =
                    outgoingProperty;
                channel.StoreInjectedKafkaMessageProperty();
            }

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            KafkaMessageProperty property = testService.KafkaMessageProperty;
            Assert.NotNull(property);
            Assert.Contains(property.Headers, x => x.Key == "header1" && Encoding.UTF8.GetString(x.Value.Span) == "header1Value");
            Assert.Equal( Encoding.UTF8.GetBytes("key").AsMemory(), property.PartitionKey);
            Assert.Equal(Topic, property.Topic);
        }

        await AssertEx.RetryAsync(() => Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic)));
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
                }, $"net.kafka://{KafkaEx.GetBootstrapServers()}/{topicNameAccessor.Invoke()}");
            });
        }
    }
}
