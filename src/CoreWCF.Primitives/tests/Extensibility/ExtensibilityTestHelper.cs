using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace CoreWCF.Primitives.Tests.Extensibility
{
    internal static class ExtensibilityTestHelper
    {
        public static void BuildDispatcherAndCallService<TService>(ServiceCollection services, TService serviceImplementation) where TService : class, ISimpleService
        {
            string serviceAddress = "http://localhost/dummy";
            services.AddSingleton(serviceImplementation);
            var serviceDispatcher = BuildDispatcher<TService>(services, serviceAddress);
            IChannel mockChannel = new MockReplyChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
        }

        public static void BuildDispatcherAndCallService(ServiceCollection services)
        {
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = BuildDispatcher<SimpleService>(services, serviceAddress);
            IChannel mockChannel = new MockReplyChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
        }

        private static IServiceDispatcher BuildDispatcher<TService>(ServiceCollection services, string serviceAddress) where TService : class, ISimpleService
        {
            services.AddServiceModelServices();
            var serverAddressesFeature = new ServerAddressesFeature();
            serverAddressesFeature.Addresses.Add(serviceAddress);
            IServer server = new MockServer();
            server.Features.Set<IServerAddressesFeature>(serverAddressesFeature);
            services.AddSingleton(server);
            var serviceProvider = services.BuildServiceProvider();
            var serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.AddService<TService>();
            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<TService, ISimpleService>(binding, serviceAddress);
            var dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            var dispatchers = dispatcherBuilder.BuildDispatchers(typeof(TService));
            return dispatchers[0];
        }
    }
}
