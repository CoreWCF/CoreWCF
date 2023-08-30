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
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Kafka.Tests;

public class KafkaSemanticsTests : IntegrationTest
{
    public KafkaSemanticsTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [LinuxWhenCIOnlyTheory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task AtLeastOnceTests(int messageCount)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupAtLeastOnce>(Output, ConsumerGroup, Topic).Build();
        using (host)
        {
            await host.StartAsync();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(messageCount);

            ServiceModel.Channels.KafkaBinding kafkaBinding = new();
            var factory = new System.ServiceModel.ChannelFactory<ITestContract>(kafkaBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://localhost:9092/{Topic}")));
            ITestContract channel = factory.CreateChannel();

            List<string> expected = new(messageCount);
            for (int i = 0; i < messageCount; i++)
            {
                string name = Guid.NewGuid().ToString();
                expected.Add(name);
                channel.Create(name);
            }

            factory.Close(TimeSpan.FromSeconds(10));

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(expected.Count, testService.Names.Count);
        }

        // await AssertEx.RetryAsync(() => Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic)));
        Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic));
    }

    [LinuxWhenCIOnlyTheory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task AtLeastOnceCommitPerMessageTests(int messageCount)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupAtLeastOnceCommitPerMessage>(Output, ConsumerGroup, Topic).Build();
        using (host)
        {
            await host.StartAsync();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(messageCount);

            ServiceModel.Channels.KafkaBinding kafkaBinding = new();
            var factory = new System.ServiceModel.ChannelFactory<ITestContract>(kafkaBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://localhost:9092/{Topic}")));
            ITestContract channel = factory.CreateChannel();

            List<string> expected = new(messageCount);
            for (int i = 0; i < messageCount; i++)
            {
                string name = Guid.NewGuid().ToString();
                expected.Add(name);
                channel.Create(name);
            }

            factory.Close(TimeSpan.FromSeconds(10));

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(expected.Count, testService.Names.Count);
        }

        // await AssertEx.RetryAsync(() => Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic)));
        Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic));
    }

    [LinuxWhenCIOnlyTheory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task AtMostOnceTests(int messageCount)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupAtMostOnce>(Output, ConsumerGroup, Topic).Build();
        using (host)
        {
            await host.StartAsync();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(messageCount);

            ServiceModel.Channels.KafkaBinding kafkaBinding = new();
            var factory = new System.ServiceModel.ChannelFactory<ITestContract>(kafkaBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://localhost:9092/{Topic}")));
            ITestContract channel = factory.CreateChannel();

            List<string> expected = new(messageCount);
            for (int i = 0; i < messageCount; i++)
            {
                string name = Guid.NewGuid().ToString();
                expected.Add(name);
                channel.Create(name);
            }

            factory.Close(TimeSpan.FromSeconds(10));

            Assert.True(testService.CountdownEvent.Wait(TimeSpan.FromSeconds(10)));
            Assert.Equal(expected.Count, testService.Names.Count);
        }

        // await AssertEx.RetryAsync(() => Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic)));
        Assert.Equal(0, KafkaEx.GetConsumerLag(Output, ConsumerGroup, Topic));
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
                }, $"net.kafka://localhost:9092/{topicNameAccessor.Invoke()}");
            });
        }
    }

    private class StartupAtLeastOnceCommitPerMessage
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
                var kafkaBinding = new KafkaBinding
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    DeliverySemantics = KafkaDeliverySemantics.AtLeastOnce,
                    GroupId = consumerGroupAccessor.Invoke()
                };
                CustomBinding customBinding = new(kafkaBinding);
                var transport = customBinding.Elements.Find<KafkaTransportBindingElement>();
                // we do support disabling auto-commit and commit synchronously per message but as this is discouraged for performance reason,
                // this configuration requires a CustomBinding
                transport.EnableAutoCommit = false;
                services.AddServiceEndpoint<TestService, ITestContract>(customBinding, $"net.kafka://localhost:9092/{topicNameAccessor.Invoke()}");
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
                }, $"net.kafka://localhost:9092/{topicNameAccessor.Invoke()}");
            });
        }
    }
}
