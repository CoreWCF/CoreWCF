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
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests;

public class PoisonPillTests : IntegrationTest
{
    private const string MessageTemplate =
        @"<s:Envelope xmlns:s=""http://www.w3.org/2003/05/soap-envelope"" xmlns:a=""http://www.w3.org/2005/08/addressing"">"
        + @"<s:Header><a:Action s:mustUnderstand=""1"">http://tempuri.org/ITestContract/Create</a:Action></s:Header>"
        + @"<s:Body><Create xmlns=""http://tempuri.org/""><name>{0}</name></Create></s:Body>"
        + @"</s:Envelope>";

    public PoisonPillTests(ITestOutputHelper output, KafkaContainerFixture containerFixture)
        : base(output, containerFixture)
    {
    }

    public static IEnumerable<object[]> Get_PoisonPill_TestVariations()
    {
        yield return new object[] { typeof(StartupAtLeastOnce) };
        yield return new object[] { typeof(StartupAtMostOnce) };
    }

    [LinuxWhenCIOnlyTheory]
    [MemberData(nameof(Get_PoisonPill_TestVariations))]
    public void PoisonPill_Tests(Type startupType)
    {
        const int messageCount = 100;
        IHost host = ServiceHelper.CreateWebHostBuilder(Output, startupType, ConsumerGroup, Topic).Build();
        using (host)
        {
            host.Start();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(messageCount - 1);

            using var producer = new ProducerBuilder<Null, string>(new ProducerConfig
                {
                    BootstrapServers = "localhost:9092",
                    Acks = Acks.All
                })
                .SetKeySerializer(Serializers.Null)
                .SetValueSerializer(Serializers.Utf8)
                .Build();

            List<string> expected = new(messageCount - 1);
            int poisonPillIndex = new Random().Next(1, messageCount - 1);
            for (int i = 0; i < messageCount; i++)
            {
                if (i == poisonPillIndex)
                {
                    string poisonPill = Guid.NewGuid().ToString();
                    producer.Produce(Topic, new Message<Null, string> { Value = poisonPill });
                }
                else
                {
                    string name = Guid.NewGuid().ToString();
                    expected.Add(name);
                    string value = string.Format(MessageTemplate, name);
                    producer.Produce(Topic, new Message<Null, string>() { Value = value });
                }
            }

            // flush
            producer.Flush(TimeSpan.FromSeconds(30));

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(30)));
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
