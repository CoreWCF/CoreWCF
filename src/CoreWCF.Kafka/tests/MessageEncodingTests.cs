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

public class MessageEncodingTests : IntegrationTest
{
    public MessageEncodingTests(ITestOutputHelper output, KafkaContainerFixture containerFixture)
        : base(output, containerFixture)
    {

    }

    [LinuxWhenCIOnlyTheory]
    [InlineData(ServiceModel.Channels.KafkaMessageEncoding.Text, typeof(Startup))]
    [InlineData(ServiceModel.Channels.KafkaMessageEncoding.Binary, typeof(StartupBinaryEncoding))]
    public void EncodingTests(ServiceModel.Channels.KafkaMessageEncoding encoding, Type startupType)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder(Output, startupType, ConsumerGroup, Topic).Build();
        using (host)
        {
            host.Start();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(1);

            ServiceModel.Channels.KafkaBinding kafkaBinding = new()
            {
                MessageEncoding = encoding
            };
            var factory = new System.ServiceModel.ChannelFactory<ITestContract>(kafkaBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://{KafkaEx.GetBootstrapServers()}/{Topic}")));
            ITestContract channel = factory.CreateChannel();

            string name = Guid.NewGuid().ToString();
            channel.Create(name);

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Contains(name, testService.Names);
        }
    }

    private class Startup
    {
        private readonly KafkaMessageEncoding _messageEncoding;

        public Startup(KafkaMessageEncoding kafkaMessageEncoding = KafkaMessageEncoding.Text)
        {
            _messageEncoding = kafkaMessageEncoding;
        }

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
                    MessageEncoding = _messageEncoding,
                    DeliverySemantics = KafkaDeliverySemantics.AtMostOnce,
                    GroupId = consumerGroupAccessor.Invoke()
                }, $"net.kafka://{KafkaEx.GetBootstrapServers()}/{topicNameAccessor.Invoke()}");
            });
        }
    }

    private class StartupBinaryEncoding : Startup
    {
        public StartupBinaryEncoding() : base(KafkaMessageEncoding.Binary)
        {

        }
    }
}
