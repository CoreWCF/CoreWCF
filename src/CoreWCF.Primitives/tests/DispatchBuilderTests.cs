// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Internal;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DispatchBuilder
{
    public static class DispatchBuilderTests
    {
        [Fact]
        public static void BuildDispatcherWithConfiguration()
        {
            string serviceAddress = "http://localhost/dummy";
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceModelServices();
            IServer server = new MockServer();
            services.AddSingleton(server);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<IApplicationLifetime, ApplicationLifetime>();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceBuilder serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.BaseAddresses.Add(new Uri(serviceAddress));
            serviceBuilder.AddService<SimpleService>();
            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleService, ISimpleService>(binding, serviceAddress);
            serviceBuilder.OpenAsync().GetAwaiter().GetResult();
            IDispatcherBuilder dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            System.Collections.Generic.List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleService));
            Assert.Single(dispatchers);
            IServiceDispatcher serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());
            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            IServiceChannelDispatcher dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext).Wait();
            Assert.True(requestContext.WaitForReply(TimeSpan.FromSeconds(5)), "Dispatcher didn't send reply");
            requestContext.ValidateReply();
        }

        [Fact]
        public static void BuildDispatcherWithConfiguration_Singleton_Not_WellKnown()
        {
            string serviceAddress = "http://localhost/dummy";
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceModelServices();
            IServer server = new MockServer();
            services.AddSingleton(server);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<IApplicationLifetime, ApplicationLifetime>();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceBuilder serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.BaseAddresses.Add(new Uri(serviceAddress));
            serviceBuilder.AddService<SimpleSingletonService>();
            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleSingletonService, ISimpleService>(binding, serviceAddress);
            serviceBuilder.OpenAsync().GetAwaiter().GetResult();
            IDispatcherBuilder dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            System.Collections.Generic.List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleSingletonService));
            Assert.Single(dispatchers);
            IServiceDispatcher serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());
            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            IServiceChannelDispatcher dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext).Wait();
            Assert.True(requestContext.WaitForReply(TimeSpan.FromSeconds(5)), "Dispatcher didn't send reply");
            requestContext.ValidateReply();
        }

        [Fact]
        public static void BuildDispatcherWithConfiguration_Singleton_WellKnown()
        {
            string serviceAddress = "http://localhost/dummy";
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceModelServices();
            IServer server = new MockServer();
            services.AddSingleton(server);
            services.AddSingleton(new SimpleSingletonService());
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<IApplicationLifetime, ApplicationLifetime>();
            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceBuilder serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.BaseAddresses.Add(new Uri(serviceAddress));
            serviceBuilder.AddService<SimpleSingletonService>();
            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleSingletonService, ISimpleService>(binding, serviceAddress);
            serviceBuilder.OpenAsync().GetAwaiter().GetResult();
            IDispatcherBuilder dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            System.Collections.Generic.List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleSingletonService));
            Assert.Single(dispatchers);
            IServiceDispatcher serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());
            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            IServiceChannelDispatcher dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext).Wait();
            Assert.True(requestContext.WaitForReply(TimeSpan.FromSeconds(5)), "Dispatcher didn't send reply");
            requestContext.ValidateReply();
        }

        [Fact]
        public static void BuildDispatcherWithConfiguration_XmlSerializer()
        {
            string serviceAddress = "http://localhost/dummy";

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceModelServices();

            IServer server = new MockServer();
            services.AddSingleton(server);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<IApplicationLifetime, ApplicationLifetime>();

            ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceBuilder serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.BaseAddresses.Add(new Uri(serviceAddress));
            serviceBuilder.AddService<SimpleXmlSerializerService>();

            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleXmlSerializerService, ISimpleXmlSerializerService>(binding, serviceAddress);
            serviceBuilder.OpenAsync().GetAwaiter().GetResult();

            IDispatcherBuilder dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            System.Collections.Generic.List<IServiceDispatcher> dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleXmlSerializerService));
            Assert.Single(dispatchers);

            IServiceDispatcher serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());

            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            IServiceChannelDispatcher dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
            var requestContext = XmlSerializerTestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext).Wait();
            Assert.True(requestContext.WaitForReply(TimeSpan.FromSeconds(5)), "Dispatcher didn't send reply");
            requestContext.ValidateReply();
        }
    }
}
