// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DispatchBuilder
{
    public class DispatchBuilderTests
    {
        private string GetAvailableServiceAddress()
        {
            return $"http://localhost:{TcpPortHelper.GetFreeTcpPort()}/dummy";
        }

        [Fact]
        public async Task BuildDispatcherWithConfiguration()
        {
            string serviceAddress = GetAvailableServiceAddress();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceModelServices();
            IServer server = new MockServer();
            services.AddSingleton(server);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.RegisterApplicationLifetime();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceBuilder serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.BaseAddresses.Add(new Uri(serviceAddress));
            serviceBuilder.AddService<SimpleService>();
            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleService, ISimpleService>(binding, serviceAddress);
            await serviceBuilder.OpenAsync();
            IDispatcherBuilder dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            System.Collections.Generic.List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleService));
            Assert.Single(dispatchers);
            IServiceDispatcher serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());
            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            IServiceChannelDispatcher dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
            var requestContext = TestRequestContext.Create(serviceAddress);
            await dispatcher.DispatchAsync(requestContext);
            Assert.True(requestContext.WaitForReply(TimeSpan.FromSeconds(30)), "Dispatcher didn't send reply");
            requestContext.ValidateReply();
        }

        [Fact]
        public async Task BuildDispatcherWithConfiguration_Singleton_Not_WellKnown()
        {
            string serviceAddress = GetAvailableServiceAddress();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceModelServices();
            IServer server = new MockServer();
            services.AddSingleton(server);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.RegisterApplicationLifetime();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceBuilder serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.BaseAddresses.Add(new Uri(serviceAddress));
            serviceBuilder.AddService<SimpleSingletonService>();
            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleSingletonService, ISimpleService>(binding, serviceAddress);
            await serviceBuilder.OpenAsync();
            IDispatcherBuilder dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            System.Collections.Generic.List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleSingletonService));
            Assert.Single(dispatchers);
            IServiceDispatcher serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());
            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            IServiceChannelDispatcher dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
            var requestContext = TestRequestContext.Create(serviceAddress);
            await dispatcher.DispatchAsync(requestContext);
            Assert.True(requestContext.WaitForReply(TimeSpan.FromSeconds(30)), "Dispatcher didn't send reply");
            requestContext.ValidateReply();
        }

        [Fact]
        public async Task BuildDispatcherWithConfiguration_Singleton_WellKnown()
        {
            string serviceAddress = GetAvailableServiceAddress();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceModelServices();
            IServer server = new MockServer();
            services.AddSingleton(server);
            services.AddSingleton(new SimpleSingletonService());
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.RegisterApplicationLifetime();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceBuilder serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.BaseAddresses.Add(new Uri(serviceAddress));
            serviceBuilder.AddService<SimpleSingletonService>();
            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleSingletonService, ISimpleService>(binding, serviceAddress);
            await serviceBuilder.OpenAsync();
            IDispatcherBuilder dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            System.Collections.Generic.List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleSingletonService));
            Assert.Single(dispatchers);
            IServiceDispatcher serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());
            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            IServiceChannelDispatcher dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
            var requestContext = TestRequestContext.Create(serviceAddress);
            await dispatcher.DispatchAsync(requestContext);
            Assert.True(requestContext.WaitForReply(TimeSpan.FromSeconds(30)), "Dispatcher didn't send reply");
            requestContext.ValidateReply();
        }

        [Fact]
        public async Task BuildDispatcherWithConfiguration_XmlSerializer()
        {
            string serviceAddress = GetAvailableServiceAddress();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceModelServices();

            IServer server = new MockServer();
            services.AddSingleton(server);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.RegisterApplicationLifetime();

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceBuilder serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.BaseAddresses.Add(new Uri(serviceAddress));
            serviceBuilder.AddService<SimpleXmlSerializerService>();

            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleXmlSerializerService, ISimpleXmlSerializerService>(binding, serviceAddress);
            await serviceBuilder.OpenAsync();

            IDispatcherBuilder dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            System.Collections.Generic.List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleXmlSerializerService));
            Assert.Single(dispatchers);

            IServiceDispatcher serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());

            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            IServiceChannelDispatcher dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
            var requestContext = XmlSerializerTestRequestContext.Create(serviceAddress);
            await dispatcher.DispatchAsync(requestContext);
            Assert.True(requestContext.WaitForReply(TimeSpan.FromSeconds(30)), "Dispatcher didn't send reply");
            requestContext.ValidateReply();
        }
    }
}
