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
using System.Xml;

namespace CoreWCF.Primitives.Tests.Helpers
{
    public static class TestHelper
    {
        private const string EchoAction = "http://tempuri.org/ISimpleService/Echo";
        private static string s_echoPrefix = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <Echo xmlns = ""http://tempuri.org/"">
      <echo>";
        private static string s_echoSuffix = @"</echo>
    </Echo>
  </s:Body>
</s:Envelope>";

        public static Message CreateEchoRequestMessage(string echo)
        {
            string requestMessageStr = s_echoPrefix + echo + s_echoSuffix;
            var xmlDictionaryReader = XmlDictionaryReader.CreateTextReader(Encoding.UTF8.GetBytes(requestMessageStr), XmlDictionaryReaderQuotas.Max);
            var requestMessage = Message.CreateMessage(xmlDictionaryReader, int.MaxValue, MessageVersion.Soap11);
            requestMessage.Headers.Action = EchoAction;
            return requestMessage;
        }

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

        public static void BuildDispatcherAndCallService<TService>(ServiceCollection services) where TService : class, ISimpleService
        {
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = BuildDispatcher<TService>(services, serviceAddress);
            IChannel mockChannel = new MockReplyChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
        }

        public static void BuildDispatcherAndCallDefaultService(ServiceCollection services)
        {
            BuildDispatcherAndCallService<SimpleService>(services);
        }

        public static IServiceDispatcher BuildDispatcher<TService>(ServiceCollection services, string serviceAddress) where TService : class, ISimpleService
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
