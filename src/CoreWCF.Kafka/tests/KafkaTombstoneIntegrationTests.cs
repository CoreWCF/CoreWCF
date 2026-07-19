// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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

namespace CoreWCF.Kafka.Tests;

// End-to-end coverage for the Kafka tombstone consume-pump halt: prove the
// consume pump survives a Kafka tombstone (Message.Value == null, a legal
// log-compaction record) and keeps processing subsequent valid messages.
// Pre-fix behaviour was that the tombstone caused ArgumentNullException out
// of OnConsumeMessage which the catch-all in the consume loop converted into
// a permanent break.
public class KafkaTombstoneIntegrationTests : IntegrationTest
{
    private const string MessageTemplate =
        @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:a=""http://www.w3.org/2005/08/addressing"">"
        + @"<s:Header><a:Action s:mustUnderstand=""1"">http://tempuri.org/ITestContract/Create</a:Action></s:Header>"
        + @"<s:Body><Create xmlns=""http://tempuri.org/""><name>{0}</name></Create></s:Body>"
        + @"</s:Envelope>";

    public KafkaTombstoneIntegrationTests(ITestOutputHelper output, KafkaContainerFixture containerFixture)
        : base(output, containerFixture)
    {
    }

    public static IEnumerable<object[]> Get_Tombstone_TestVariations()
    {
        yield return new object[] { typeof(StartupAtLeastOnce) };
        yield return new object[] { typeof(StartupAtMostOnce) };
    }

    [LinuxWhenCIOnlyTheory]
    [MemberData(nameof(Get_Tombstone_TestVariations))]
    public void Tombstone_DoesNotHaltConsumePump(Type startupType)
    {
        const int validMessageCount = 10;
        IHost host = ServiceHelper.CreateHost(Output, startupType, ConsumerGroup, Topic);
        using (host)
        {
            host.Start();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(validMessageCount);

            // Producer 1: bytes-typed so we can publish a real tombstone (Value=null).
            using var bytesProducer = new ProducerBuilder<string, byte[]>(new ProducerConfig
                {
                    BootstrapServers = KafkaEx.GetBootstrapServers(),
                    Acks = Acks.All
                })
                .SetKeySerializer(Serializers.Utf8)
                .SetValueSerializer(Serializers.ByteArray)
                .Build();

            // Producer 2: text-typed for the valid SOAP envelopes (matches PoisonPillTests).
            using var textProducer = new ProducerBuilder<Null, string>(new ProducerConfig
                {
                    BootstrapServers = KafkaEx.GetBootstrapServers(),
                    Acks = Acks.All
                })
                .SetKeySerializer(Serializers.Null)
                .SetValueSerializer(Serializers.Utf8)
                .Build();

            // First: send a tombstone (legal log-compaction record).
            bytesProducer.Produce(Topic, new Message<string, byte[]>
            {
                Key = "tombstone-regression",
                Value = null,
            });
            bytesProducer.Flush(TimeSpan.FromSeconds(30));

            // Then: send valid messages. Pre-fix the pump is dead and none arrive.
            List<string> expected = new(validMessageCount);
            for (int i = 0; i < validMessageCount; i++)
            {
                string name = Guid.NewGuid().ToString();
                expected.Add(name);
                string value = string.Format(MessageTemplate, name);
                textProducer.Produce(Topic, new Message<Null, string> { Value = value });
            }
            textProducer.Flush(TimeSpan.FromSeconds(30));

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(30)),
                "Tombstone regression: consume pump halted after a single tombstone; "
                + "subsequent valid messages were never dispatched.");
            Assert.Equal(expected.Count, testService.Names.Count);
        }
    }

    private class StartupAtLeastOnce
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
                services.AddServiceEndpoint<TestService, ITestContract>(new KafkaBinding
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    DeliverySemantics = KafkaDeliverySemantics.AtLeastOnce,
                    GroupId = consumerGroupAccessor.Invoke()
                }, $"net.kafka://{KafkaEx.GetBootstrapServers()}/{topicNameAccessor.Invoke()}");
            });
        }
    }

    private class StartupAtMostOnce
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
