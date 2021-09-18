// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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

        public MessageEncodingTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> GetTestVariations()
        {
            yield return new object[] { typeof(BasicHttpBindingWithTextMessageEncodingStartup), BasicHttpBindingWithTextMessageEncodingStartup.GetClientBinding() };
            yield return new object[] { typeof(BasicHttpBindingWithMtomMessageEncodingStartup), BasicHttpBindingWithMtomMessageEncodingStartup.GetClientBinding() };
            yield return new object[] { typeof(WSHttpBindingWithTextMessageEncodingStartup), WSHttpBindingWithTextMessageEncodingStartup.GetClientBinding() };
            yield return new object[] { typeof(WSHttpBindingWithMtomMessageEncodingStartup), WSHttpBindingWithMtomMessageEncodingStartup.GetClientBinding() };
            yield return new object[] { typeof(NetHttpBindingWithTextMessageEncodingStartup), NetHttpBindingWithTextMessageEncodingStartup.GetClientBinding() };
            yield return new object[] { typeof(NetHttpBindingWithMtomMessageEncodingStartup), NetHttpBindingWithMtomMessageEncodingStartup.GetClientBinding() };
            yield return new object[] { typeof(NetHttpBindingWithBinaryMessageEncodingStartup), NetHttpBindingWithBinaryMessageEncodingStartup.GetClientBinding() };
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public void EchoByteArray(Type startupType, System.ServiceModel.Channels.Binding binding)
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
            using (host)
            {
                host.Start();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IMessageEncodingService>(binding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/MessageEncodingService/IMessageEncodingService.svc")));
                ClientContract.IMessageEncodingService channel = factory.CreateChannel();
                byte[] bytes = new byte[768];
#if NET472_OR_GREATER
                using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                {
                    rng.GetBytes(bytes);
                }
#else
                RandomNumberGenerator.Fill(bytes);
#endif
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

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
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
    }
}
