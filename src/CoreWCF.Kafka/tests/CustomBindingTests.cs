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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests;

public class CustomBindingTests : IntegrationTest
{
    private const string MessageTemplate =
        @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:a=""http://www.w3.org/2005/08/addressing"">"
        + @"<s:Header><a:Action s:mustUnderstand=""1"">http://tempuri.org/ITestContract/Create</a:Action></s:Header>"
        + @"<s:Body><Create xmlns=""http://tempuri.org/""><name>{0}</name></Create></s:Body>"
        + @"</s:Envelope>";

    public CustomBindingTests(ITestOutputHelper output, KafkaContainerFixture containerFixture)
        : base(output, containerFixture)
    {

    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaProducer_WithServerSideCustomBinding_Test()
    {
        IHost host = ServiceHelper.CreateHost<Startup>(Output, ConsumerGroup, Topic);
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

            string name = Guid.NewGuid().ToString();
            string value = string.Format(MessageTemplate, name);
            var result = await producer.ProduceAsync(Topic, new Message<Null, string> { Value = value });

            Assert.True(result.Status == PersistenceStatus.Persisted);

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Contains(name, testService.Names);
        }

        Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic));
    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaClientBinding_WithServerSideCustomBinding_Test()
    {
        IHost host = ServiceHelper.CreateHost<Startup>(Output, ConsumerGroup, Topic);
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

            string name = Guid.NewGuid().ToString();
            await channel.CreateAsync(name);

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Contains(name, testService.Names);
        }

        Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic));
    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaClientCustomBinding_WithServerSideCustomBinding_Test()
    {
        IHost host = ServiceHelper.CreateHost<Startup>(Output, ConsumerGroup, Topic);
        using (host)
        {
            await host.StartAsync();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(1);

            ServiceModel.Channels.KafkaBinding kafkaBinding = new();
            System.ServiceModel.Channels.CustomBinding customBinding = new(kafkaBinding);
            ServiceModel.Channels.KafkaTransportBindingElement transport =
                customBinding.Elements.Find<ServiceModel.Channels.KafkaTransportBindingElement>();
            transport.CompressionType = CompressionType.Snappy;
            var factory = new System.ServiceModel.ChannelFactory<ITestContract>(customBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://{KafkaEx.GetBootstrapServers()}/{Topic}")));
            ITestContract channel = factory.CreateChannel();

            string name = Guid.NewGuid().ToString();
            await channel.CreateAsync(name);

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Contains(name, testService.Names);
        }

        Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic));
    }

    private class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<TestService>();
            services.AddServiceModelServices();
            services.AddQueueTransport();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(services =>
            {
                var topicNameAccessor = app.ApplicationServices.GetService<TopicNameAccessor>();
                var consumerGroupAccessor = app.ApplicationServices.GetService<ConsumerGroupAccessor>();
                services.AddService<TestService>();
                var binding = new KafkaBinding(KafkaDeliverySemantics.AtMostOnce)
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    GroupId = consumerGroupAccessor.Invoke()
                };
                var customBinding = new CustomBinding(binding);
                KafkaTransportBindingElement transport = customBinding.Elements.Find<KafkaTransportBindingElement>();
                transport.Debug = "all";
                services.AddServiceEndpoint<TestService, ITestContract>(customBinding, $"net.kafka://{KafkaEx.GetBootstrapServers()}/{topicNameAccessor.Invoke()}");
            });
        }
    }
}
