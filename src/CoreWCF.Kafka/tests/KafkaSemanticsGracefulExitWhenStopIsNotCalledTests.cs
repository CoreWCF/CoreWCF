// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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

public class KafkaSemanticsGracefulExitWhenStopIsNotCalledTests : IntegrationTest
{
    public KafkaSemanticsGracefulExitWhenStopIsNotCalledTests(ITestOutputHelper output)
        : base(output)
    {
    }

    [LinuxWhenCIOnlyTheory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task AtLeastOnceTests(int messageCount)
    {
        StopCountHolder stopCountHolder = new();
        WebApplication webApplication = ServiceHelper.CreateWebApplication(Output, ConsumerGroup, Topic,
            services =>
            {
                StartupAtLeastOnce.ConfigureServices(services);
                services.AddSingleton(_ => stopCountHolder);
            }, StartupAtLeastOnce.Configure);
        await using (webApplication)
        {
            await webApplication.StartAsync();
            var resolver = new DependencyResolverHelper(webApplication.Services);
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

        // We need to wait a bit to be sure offsets are committed
        await Task.Delay(TimeSpan.FromSeconds(10));

        Assert.Equal(0, await KafkaEx.GetConsumerLagAsync(Output, ConsumerGroup, Topic));
        Assert.Equal(0, stopCountHolder.Value);
    }

    [LinuxWhenCIOnlyTheory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task AtLeastOnceCommitPerMessageTests(int messageCount)
    {
        StopCountHolder stopCountHolder = new();
        WebApplication webApplication = ServiceHelper.CreateWebApplication(Output, ConsumerGroup, Topic,
            services =>
            {
                StartupAtLeastOnceCommitPerMessage.ConfigureServices(services);
                services.AddSingleton(_ => stopCountHolder);
            }, StartupAtLeastOnceCommitPerMessage.Configure);
        await using (webApplication)
        {
            await webApplication.StartAsync();
            var resolver = new DependencyResolverHelper(webApplication.Services);
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

        // We need to wait a bit to be sure offsets are committed
        await Task.Delay(TimeSpan.FromSeconds(10));

        Assert.Equal(0, await KafkaEx.GetConsumerLagAsync(Output, ConsumerGroup, Topic));
        Assert.Equal(0, stopCountHolder.Value);
    }

    [LinuxWhenCIOnlyTheory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public async Task AtMostOnceTests(int messageCount)
    {
        StopCountHolder stopCountHolder = new();
        WebApplication webApplication = ServiceHelper.CreateWebApplication(Output, ConsumerGroup, Topic,
            services =>
            {
                StartupAtLeastOnce.ConfigureServices(services);
                services.AddSingleton(_ => stopCountHolder);
            }, StartupAtLeastOnce.Configure);
        await using (webApplication)
        {
            await webApplication.StartAsync();
            var resolver = new DependencyResolverHelper(webApplication.Services);
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

        // We need to wait a bit to be sure offsets are committed
        await Task.Delay(TimeSpan.FromSeconds(10));

        Assert.Equal(0, await KafkaEx.GetConsumerLagAsync(Output, ConsumerGroup, Topic));
        Assert.Equal(0, stopCountHolder.Value);
    }

    private static class StartupAtLeastOnce
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
                var consumerGroupAccessor = app.Services.GetService<ConsumerGroupAccessor>();
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

    private static class StartupAtLeastOnceCommitPerMessage
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
                var consumerGroupAccessor = app.Services.GetService<ConsumerGroupAccessor>();
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


    private static class StartupAtMostOnce
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
                var consumerGroupAccessor = app.Services.GetService<ConsumerGroupAccessor>();
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
