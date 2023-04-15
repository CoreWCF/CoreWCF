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

        public static IEnumerable<object[]> GetTestVariations()
        {
            yield return new object[] { typeof(BasicHttpBindingWithTextMessageEncodingStartup), BasicHttpBindingWithTextMessageEncodingStartup.GetClientBinding(), LargeRequestByteArrayLength };
            yield return new object[] { typeof(BasicHttpBindingWithMtomMessageEncodingStartup), BasicHttpBindingWithMtomMessageEncodingStartup.GetClientBinding(), LargeRequestByteArrayLength, new AssertMtomOptimizedEndpointBehavior() };
            yield return new object[] { typeof(WSHttpBindingWithTextMessageEncodingStartup), WSHttpBindingWithTextMessageEncodingStartup.GetClientBinding(), LargeRequestByteArrayLength };
            yield return new object[] { typeof(WSHttpBindingWithMtomMessageEncodingStartup), WSHttpBindingWithMtomMessageEncodingStartup.GetClientBinding(), LargeRequestByteArrayLength, new AssertMtomOptimizedEndpointBehavior() };
            yield return new object[] { typeof(NetHttpBindingWithTextMessageEncodingStartup), NetHttpBindingWithTextMessageEncodingStartup.GetClientBinding(), LargeRequestByteArrayLength };
            yield return new object[] { typeof(NetHttpBindingWithMtomMessageEncodingStartup), NetHttpBindingWithMtomMessageEncodingStartup.GetClientBinding(), LargeRequestByteArrayLength, new AssertMtomOptimizedEndpointBehavior() };
            yield return new object[] { typeof(NetHttpBindingWithBinaryMessageEncodingStartup), NetHttpBindingWithBinaryMessageEncodingStartup.GetClientBinding(), LargeRequestByteArrayLength };

            yield return new object[] { typeof(BasicHttpBindingWithTextMessageEncodingStartup), BasicHttpBindingWithTextMessageEncodingStartup.GetClientBinding(), SmallRequestByteArrayLength };
            yield return new object[] { typeof(BasicHttpBindingWithMtomMessageEncodingStartup), BasicHttpBindingWithMtomMessageEncodingStartup.GetClientBinding(), SmallRequestByteArrayLength };
            yield return new object[] { typeof(WSHttpBindingWithTextMessageEncodingStartup), WSHttpBindingWithTextMessageEncodingStartup.GetClientBinding(), SmallRequestByteArrayLength };
            yield return new object[] { typeof(WSHttpBindingWithMtomMessageEncodingStartup), WSHttpBindingWithMtomMessageEncodingStartup.GetClientBinding(), SmallRequestByteArrayLength };
            yield return new object[] { typeof(NetHttpBindingWithTextMessageEncodingStartup), NetHttpBindingWithTextMessageEncodingStartup.GetClientBinding(), SmallRequestByteArrayLength };
            yield return new object[] { typeof(NetHttpBindingWithMtomMessageEncodingStartup), NetHttpBindingWithMtomMessageEncodingStartup.GetClientBinding(), SmallRequestByteArrayLength };
            yield return new object[] { typeof(NetHttpBindingWithBinaryMessageEncodingStartup), NetHttpBindingWithBinaryMessageEncodingStartup.GetClientBinding(), SmallRequestByteArrayLength };
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public void EchoByteArray(Type startupType, System.ServiceModel.Channels.Binding binding, int bytesCount, System.ServiceModel.Description.IEndpointBehavior endpointBehavior = null)
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
            using (host)
            {
                host.Start();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IMessageEncodingService>(binding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/MessageEncodingService/IMessageEncodingService.svc")));
                if (endpointBehavior != null)
                {
                    factory.Endpoint.EndpointBehaviors.Add(endpointBehavior);
                }

                ClientContract.IMessageEncodingService channel = factory.CreateChannel();
                var bytes = ClientHelper.GetByteArray(bytesCount);
                var result = channel.EchoByteArray(bytes);
                Assert.Equal(bytes, result);
            }
        }

        internal abstract class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.MessageEncodingService>();
                    builder.AddServiceEndpoint<Services.MessageEncodingService, ServiceContract.IMessageEncodingService>(GetServerBinding(), $"/MessageEncodingService/IMessageEncodingService.svc");
                });
            }

            protected abstract Channels.Binding GetServerBinding();
        }

        internal class BasicHttpBindingWithTextMessageEncodingStartup : Startup
        {
            internal static System.ServiceModel.Channels.Binding GetClientBinding()
            {
                var binding = ClientHelper.GetBufferedModeBinding();
                binding.MessageEncoding = System.ServiceModel.WSMessageEncoding.Text;
                return binding;
            }

            protected override Channels.Binding GetServerBinding() => new BasicHttpBinding()
            {
                MessageEncoding = CoreWCF.WSMessageEncoding.Text
            };
        }


        internal class BasicHttpBindingWithMtomMessageEncodingStartup : Startup
        {
            internal static System.ServiceModel.Channels.Binding GetClientBinding()
            {
                var binding = ClientHelper.GetBufferedModeBinding();
                binding.MessageEncoding = System.ServiceModel.WSMessageEncoding.Mtom;
                return binding;
            }
            protected override Channels.Binding GetServerBinding() => new BasicHttpBinding()
            {
                MessageEncoding = CoreWCF.WSMessageEncoding.Mtom
            };
        }


        internal class WSHttpBindingWithTextMessageEncodingStartup : Startup
        {
            internal static System.ServiceModel.Channels.Binding GetClientBinding()
            {
                var binding = ClientHelper.GetBufferedModeWSHttpBinding(securityMode: System.ServiceModel.SecurityMode.None);
                binding.MessageEncoding = System.ServiceModel.WSMessageEncoding.Text;
                return binding;
            }

            protected override Channels.Binding GetServerBinding() => new WSHttpBinding(SecurityMode.None)
            {
                MessageEncoding = CoreWCF.WSMessageEncoding.Text
            };
        }

        internal class WSHttpBindingWithMtomMessageEncodingStartup : Startup
        {
            internal static System.ServiceModel.Channels.Binding GetClientBinding()
            {
                var binding = ClientHelper.GetBufferedModeWSHttpBinding(securityMode: System.ServiceModel.SecurityMode.None);
                binding.MessageEncoding = System.ServiceModel.WSMessageEncoding.Mtom;
                return binding;
            }

            protected override Channels.Binding GetServerBinding() => new WSHttpBinding(SecurityMode.None)
            {
                MessageEncoding = CoreWCF.WSMessageEncoding.Mtom
            };
        }

        internal class NetHttpBindingWithTextMessageEncodingStartup : Startup
        {
            internal static System.ServiceModel.Channels.Binding GetClientBinding()
            {
                var binding = ClientHelper.GetBufferedModeWebSocketBinding();
                binding.MessageEncoding = System.ServiceModel.NetHttpMessageEncoding.Text;
                return binding;
            }

            protected override Channels.Binding GetServerBinding()
            {
                var binding = new NetHttpBinding(Channels.BasicHttpSecurityMode.None);
                binding.MessageEncoding = NetHttpMessageEncoding.Text;
                return binding;
            }
        }

        internal class NetHttpBindingWithMtomMessageEncodingStartup : Startup
        {
            internal static System.ServiceModel.Channels.Binding GetClientBinding()
            {
                var binding = ClientHelper.GetBufferedModeWebSocketBinding();
                binding.MessageEncoding = System.ServiceModel.NetHttpMessageEncoding.Mtom;
                return binding;
            }

            protected override Channels.Binding GetServerBinding()
            {
                var binding = new NetHttpBinding(Channels.BasicHttpSecurityMode.None);
                binding.MessageEncoding = NetHttpMessageEncoding.Mtom;
                return binding;
            }
        }

        internal class NetHttpBindingWithBinaryMessageEncodingStartup : Startup
        {
            internal static System.ServiceModel.Channels.Binding GetClientBinding()
            {
                var binding = ClientHelper.GetBufferedModeWebSocketBinding();
                binding.MessageEncoding = System.ServiceModel.NetHttpMessageEncoding.Binary;
                return binding;
            }

            protected override Channels.Binding GetServerBinding()
            {
                var binding = new NetHttpBinding(Channels.BasicHttpSecurityMode.None);
                binding.MessageEncoding = NetHttpMessageEncoding.Binary;
                return binding;
            }
        }

        internal class AssertMtomOptimizedEndpointBehavior : System.ServiceModel.Description.IEndpointBehavior
        {
            class AssertMtomOptimizedDelegatingHandler : DelegatingHandler
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
