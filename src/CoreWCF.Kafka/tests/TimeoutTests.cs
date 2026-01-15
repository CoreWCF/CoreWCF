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

public class TimeoutTests : IntegrationTest
{
    public TimeoutTests(ITestOutputHelper output, KafkaContainerFixture containerFixture)
        : base(output, containerFixture)
    {

    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaClientBindingTest()
    {
        IHost host = ServiceHelper.CreateHost<Startup>(Output, ConsumerGroup, Topic);
        using (host)
        {
            await host.StartAsync();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(1);

            await KafkaEx.PauseAsync(Output);
            try
            {
                async Task Act()
                {
                    ServiceModel.Channels.KafkaBinding kafkaBinding = new();
                    kafkaBinding.SendTimeout = TimeSpan.FromSeconds(5);
                    var factory = new System.ServiceModel.ChannelFactory<ITestContract>(kafkaBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://{KafkaEx.GetBootstrapServers()}/{Topic}")));
                    ITestContract channel = factory.CreateChannel();
                    await channel.CreateAsync(Guid.NewGuid().ToString());
                }

                var timeoutException = await Assert.ThrowsAsync<TimeoutException>(Act);

                Assert.NotNull(timeoutException);
            }
            finally
            {
                await KafkaEx.UnpauseAsync(Output);
            }
        }
    }

    [LinuxWhenCIOnlyFact]
    public async Task KafkaClientBindingCustomBindingTest()
    {
        IHost host = ServiceHelper.CreateHost<Startup>(Output, ConsumerGroup, Topic);
        using (host)
        {
            await host.StartAsync();
            var resolver = new DependencyResolverHelper(host);
            var testService = resolver.GetService<TestService>();
            testService.CountdownEvent.Reset(1);

            await KafkaEx.PauseAsync(Output);
            try
            {
                async Task Act()
                {
                    ServiceModel.Channels.KafkaBinding kafkaBinding = new();
                    System.ServiceModel.Channels.CustomBinding customBinding = new(kafkaBinding);
                    var transportBindingElement = customBinding.Elements.Find<ServiceModel.Channels.KafkaTransportBindingElement>();
                    transportBindingElement.MessageTimeoutMs = 5000;
                    var factory = new System.ServiceModel.ChannelFactory<ITestContract>(kafkaBinding,
                        new System.ServiceModel.EndpointAddress(new Uri($"net.kafka://{KafkaEx.GetBootstrapServers()}/{Topic}")));
                    ITestContract channel = factory.CreateChannel();
                    await channel.CreateAsync(Guid.NewGuid().ToString());
                }

                var timeoutException = await Assert.ThrowsAsync<TimeoutException>(Act);

                Assert.NotNull(timeoutException);
            }
            finally
            {
                await KafkaEx.UnpauseAsync(Output);
            }
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

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(services =>
            {
                var topicNameAccessor = app.ApplicationServices.GetService<TopicNameAccessor>();
                var consumerGroupAccessor = app.ApplicationServices.GetService<ConsumerGroupAccessor>();
                services.AddService<TestService>();
                var binding = new KafkaBinding
                {
                    AutoOffsetReset = AutoOffsetReset.Earliest,
                    DeliverySemantics = KafkaDeliverySemantics.AtMostOnce,
                    GroupId = consumerGroupAccessor.Invoke()
                };
                var customBinding = new CustomBinding(binding);
                KafkaTransportBindingElement transport = customBinding.Elements.Find<KafkaTransportBindingElement>();
                transport.Debug = "consumer";
                transport.SessionTimeoutMs = 60000 + 10000;
                services.AddServiceEndpoint<TestService, ITestContract>(customBinding, $"net.kafka://{KafkaEx.GetBootstrapServers()}/{topicNameAccessor.Invoke()}");
            });
        }
    }
}
