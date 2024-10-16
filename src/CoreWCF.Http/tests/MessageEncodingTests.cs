// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class MessageEncodingTests
    {
        private readonly ITestOutputHelper _output;

        private const int LargeRequestByteArrayLength = 1024;
        private const int SmallRequestByteArrayLength = 10;

        public MessageEncodingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("BasicHttpBinding", "Text", LargeRequestByteArrayLength)]
        [InlineData("BasicHttpBinding", "Mtom", LargeRequestByteArrayLength)]
        [InlineData("WSHttpBinding", "Text", LargeRequestByteArrayLength)]
        [InlineData("WSHttpBinding", "Mtom", LargeRequestByteArrayLength)]
        [InlineData("WS2007HttpBinding", "Text", LargeRequestByteArrayLength)]
        [InlineData("WS2007HttpBinding", "Mtom", LargeRequestByteArrayLength)]
        [InlineData("NetHttpBinding", "Text", LargeRequestByteArrayLength)]
        [InlineData("NetHttpBinding", "Mtom", LargeRequestByteArrayLength)]
        [InlineData("NetHttpBinding", "Binary", LargeRequestByteArrayLength)]
        [InlineData("BasicHttpBinding", "Text", SmallRequestByteArrayLength)]
        [InlineData("BasicHttpBinding", "Mtom", SmallRequestByteArrayLength)]
        [InlineData("WSHttpBinding", "Text", SmallRequestByteArrayLength)]
        [InlineData("WSHttpBinding", "Mtom", SmallRequestByteArrayLength)]
        [InlineData("WS2007HttpBinding", "Text", SmallRequestByteArrayLength)]
        [InlineData("WS2007HttpBinding", "Mtom", SmallRequestByteArrayLength)]
        [InlineData("NetHttpBinding", "Text", SmallRequestByteArrayLength)]
        [InlineData("NetHttpBinding", "Mtom", SmallRequestByteArrayLength)]
        [InlineData("NetHttpBinding", "Binary", SmallRequestByteArrayLength)]
        public void EchoByteArray(string bindingType, string messageEncoding, int bytesCount)
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output)
                .UseSetting("bindingType", bindingType)
                .UseSetting("messageEncoding", messageEncoding)
                .Build();

            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IMessageEncodingService> factory = null;
                ClientContract.IMessageEncodingService channel = null;
                host.Start();
                try
                {
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IMessageEncodingService>(GetClientBinding(bindingType, messageEncoding),
                        new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/MessageEncodingService/IMessageEncodingService.svc")));

                    if (messageEncoding == "Mtom" && bytesCount == LargeRequestByteArrayLength)
                    {
                        factory.Endpoint.EndpointBehaviors.Add(new AssertMtomOptimizedEndpointBehavior());
                    }

                    channel = factory.CreateChannel();
                    var bytes = ClientHelper.GetByteArray(bytesCount);
                    var result = channel.EchoByteArray(bytes);
                    Assert.Equal(bytes, result);
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        internal static T ParseEnum<T>(string value) => (T)Enum.Parse(typeof(T), value);

        private static CoreWCF.Channels.Binding GetServerBinding(string bindingType, string messageEncoding)
        {
            CoreWCF.Channels.Binding binding;
            if (bindingType == "BasicHttpBinding")
            {
                var basicHttpBinding = new CoreWCF.BasicHttpBinding();
                basicHttpBinding.MessageEncoding = ParseEnum<CoreWCF.WSMessageEncoding>(messageEncoding);
                binding = basicHttpBinding;
            }
            else if (bindingType == "WSHttpBinding")
            {
                var wsHttpbinding = new CoreWCF.WSHttpBinding(SecurityMode.None);
                wsHttpbinding.MessageEncoding = ParseEnum<CoreWCF.WSMessageEncoding>(messageEncoding);
                binding = wsHttpbinding;
            }
            else if (bindingType == "WS2007HttpBinding")
            {
                var ws2007Httpbinding = new CoreWCF.WS2007HttpBinding(SecurityMode.None);
                ws2007Httpbinding.MessageEncoding = ParseEnum<CoreWCF.WSMessageEncoding>(messageEncoding);
                binding = ws2007Httpbinding;
            }
            else
            {
                var netHttpBinding = new CoreWCF.NetHttpBinding(Channels.BasicHttpSecurityMode.None);
                netHttpBinding.MessageEncoding = ParseEnum<CoreWCF.NetHttpMessageEncoding>(messageEncoding);
                binding = netHttpBinding;
            }

            return binding;
        }

        private static System.ServiceModel.Channels.Binding GetClientBinding(string bindingType, string messageEncoding)
        {
            System.ServiceModel.Channels.Binding binding;
            if (bindingType == "BasicHttpBinding")
            {
                var basicHttpBinding = ClientHelper.GetBufferedModeBinding();
                basicHttpBinding.MessageEncoding = ParseEnum<System.ServiceModel.WSMessageEncoding>(messageEncoding);
                binding = basicHttpBinding;
            }
            else if (bindingType == "WSHttpBinding" || bindingType == "WS2007HttpBinding")
            {
                var wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(bindingType, securityMode: System.ServiceModel.SecurityMode.None);
                wsHttpBinding.MessageEncoding = ParseEnum<System.ServiceModel.WSMessageEncoding>(messageEncoding);
                binding = wsHttpBinding;
            }
            else
            {
                var netHttpBinding = ClientHelper.GetBufferedModeWebSocketBinding();
                netHttpBinding.MessageEncoding = ParseEnum<System.ServiceModel.NetHttpMessageEncoding>(messageEncoding);
                binding = netHttpBinding;
            }

            return binding;
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                var config = app.ApplicationServices.GetRequiredService<IConfiguration>();

                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.MessageEncodingService>();
                    builder.AddServiceEndpoint<Services.MessageEncodingService, ServiceContract.IMessageEncodingService>(
                        GetServerBinding(config["bindingType"], config["messageEncoding"]), $"/MessageEncodingService/IMessageEncodingService.svc");
                });
            }
        }

        internal class AssertMtomOptimizedEndpointBehavior : System.ServiceModel.Description.IEndpointBehavior
        {
            private class AssertMtomOptimizedDelegatingHandler : DelegatingHandler
            {
                public AssertMtomOptimizedDelegatingHandler(HttpMessageHandler innerHandler) : base(innerHandler)
                {
                }

                protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                {
                    var response = await base.SendAsync(request, cancellationToken);
                    var content = await response.Content.ReadAsStringAsync();
                    Assert.Contains("<xop:Include", content);
                    return response;
                }
            }

            public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
            {
                bindingParameters.Add(new Func<HttpClientHandler, HttpMessageHandler>(GetHttpMessageHandler));
            }

            public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
            {

            }

            public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
            {

            }

            public void Validate(ServiceEndpoint endpoint)
            {

            }

            private HttpMessageHandler GetHttpMessageHandler(HttpClientHandler httpClientHandler) => new AssertMtomOptimizedDelegatingHandler(httpClientHandler);
        }
    }
}
