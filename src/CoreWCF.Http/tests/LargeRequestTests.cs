// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class LargeRequestTests
    {
        private readonly ITestOutputHelper _output;

        public LargeRequestTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public void EchoRoundtrip(Type startupType, System.ServiceModel.TransferMode clientTransferMode, int requestSize, bool allowSynchronhousIO)
        {
            string testString = new string('a', requestSize);
            var hostBuilder = ServiceHelper.CreateWebHostBuilder(_output, startupType);
            if (allowSynchronhousIO)
            {
                hostBuilder.ConfigureServices(services =>
                {
                    services.Configure<KestrelServerOptions>(options =>
                    {
                        options.AllowSynchronousIO = true;
                    });
                });
            }
            IWebHost host = hostBuilder.Build();
            using (host)
            {
                host.Start();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(Startup.GetClientBinding(clientTransferMode),
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                ((System.ServiceModel.Channels.IChannel)channel).Close();
                factory.Close();
            }
        }

        public static IEnumerable<object[]> GetTestVariations()
        {
            foreach (int requestSize in new int[] { 1024, 1024 * 1024, 10 * 1024 * 1024 })
            {
                foreach (TransferMode transferMode in new TransferMode[] { TransferMode.Buffered, TransferMode.Streamed })
                {
                    foreach (System.ServiceModel.TransferMode clientTransferMode in new System.ServiceModel.TransferMode[] { System.ServiceModel.TransferMode.Buffered, System.ServiceModel.TransferMode.Streamed })
                    {
                        switch (transferMode)
                        {
                            case TransferMode.Buffered:
                                yield return new object[] { typeof(BufferedModeStartup), clientTransferMode, requestSize, false };
                                break;
                            case TransferMode.Streamed:
                                yield return new object[] { typeof(StreamedModeStartup), clientTransferMode, requestSize, true };
                                break;
                        }
                    }
                }
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
                    builder.AddService<Services.EchoService>();
                    var binding = new CustomBinding();
                    binding.Elements.Add(new TextMessageEncodingBindingElement { ReaderQuotas = XmlDictionaryReaderQuotas.Max });
                    binding.Elements.Add(new HttpTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, TransferMode = TransferMode });
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(binding, "/BasicWcfService/basichttp.svc");
                });
            }

            public static System.ServiceModel.Channels.Binding GetClientBinding(System.ServiceModel.TransferMode transferMode)
            {
                var binding = new System.ServiceModel.Channels.CustomBinding();
                binding.Elements.Add(new System.ServiceModel.Channels.TextMessageEncodingBindingElement { ReaderQuotas = XmlDictionaryReaderQuotas.Max });
                binding.Elements.Add(new System.ServiceModel.Channels.HttpTransportBindingElement { MaxReceivedMessageSize = int.MaxValue, TransferMode = transferMode });
                return binding;
            }

            protected abstract TransferMode TransferMode { get; }
        }

        internal class StreamedModeStartup : Startup
        {
            protected override TransferMode TransferMode => TransferMode.Streamed;
        }

        internal class BufferedModeStartup : Startup
        {
            protected override TransferMode TransferMode => TransferMode.Buffered;
        }
    }
}
