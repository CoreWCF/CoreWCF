using Helpers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using System.Threading;
using Xunit;
using System;

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
            var serverAddressesFeature = new ServerAddressesFeature();
            serverAddressesFeature.Addresses.Add(serviceAddress);
            IServer server = new MockServer();
            server.Features.Set<IServerAddressesFeature>(serverAddressesFeature);
            services.AddSingleton(server);
            var serviceProvider = services.BuildServiceProvider();
            var serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.AddService<SimpleService>();
            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleService, ISimpleService>(binding, serviceAddress);
            var dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            var dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleService));
            Assert.Single(dispatchers);
            var serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());
            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
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
            var serverAddressesFeature = new ServerAddressesFeature();
            serverAddressesFeature.Addresses.Add(serviceAddress);
            IServer server = new MockServer();
            server.Features.Set<IServerAddressesFeature>(serverAddressesFeature);
            services.AddSingleton(server);
            var serviceProvider = services.BuildServiceProvider();
            var serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.AddService<SimpleSingletonService>();
            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleSingletonService, ISimpleService>(binding, serviceAddress);
            var dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            var dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleSingletonService));
            Assert.Single(dispatchers);
            var serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());
            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
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
            var serverAddressesFeature = new ServerAddressesFeature();
            serverAddressesFeature.Addresses.Add(serviceAddress);
            IServer server = new MockServer();
            server.Features.Set<IServerAddressesFeature>(serverAddressesFeature);
            services.AddSingleton(server);
            services.AddSingleton(new SimpleSingletonService());
            var serviceProvider = services.BuildServiceProvider();
            var serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.AddService<SimpleSingletonService>();
            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleSingletonService, ISimpleService>(binding, serviceAddress);
            var dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            var dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleSingletonService));
            Assert.Single(dispatchers);
            var serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());
            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext).Wait();
            Assert.True(requestContext.WaitForReply(TimeSpan.FromSeconds(5)), "Dispatcher didn't send reply");
            requestContext.ValidateReply();
        }

        [Fact]
        public static void BuildDispatcherWithConfiguration_XmlSerializer()
        {
            var serviceAddress = "http://localhost/dummy";

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceModelServices();

            var serverAddressesFeature = new ServerAddressesFeature();
            serverAddressesFeature.Addresses.Add(serviceAddress);

            IServer server = new MockServer();
            server.Features.Set<IServerAddressesFeature>(serverAddressesFeature);
            services.AddSingleton(server);

            var serviceProvider = services.BuildServiceProvider();
            var serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.AddService<SimpleXmlSerializerService>();

            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleXmlSerializerService, ISimpleXmlSerializerService>(binding, serviceAddress);

            var dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            var dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleXmlSerializerService));
            Assert.Single(dispatchers);

            var serviceDispatcher = dispatchers[0];
            Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
            Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());

            IChannel mockChannel = new MockReplyChannel(serviceProvider);
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel).Result;
            var requestContext = XmlSerializerTestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext).Wait();
            Assert.True(requestContext.WaitForReply(TimeSpan.FromSeconds(5)), "Dispatcher didn't send reply");
            requestContext.ValidateReply();
        }
    }
}
