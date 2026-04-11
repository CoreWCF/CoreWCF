// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace CoreWCF.Http.Tests
{
    public class ReliableSessionsTests
    {
        private readonly ITestOutputHelper _output;

        public ReliableSessionsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public void WSHttpBindingSimpleReliableSessions(ReliableMessagingVersion rmVersion, System.ServiceModel.ReliableMessagingVersion clientRmVersion, bool ordered)
        {
            string testString = new('a', 3000);
            var startupFilter = new RSStartup(rmVersion, ordered);
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding("WSHttpBinding", System.ServiceModel.SecurityMode.None);
                wsHttpBinding.ReliableSession.Enabled = true;
                wsHttpBinding.ReliableSession.Ordered = ordered;
                System.ServiceModel.Channels.CustomBinding customBinding = new(wsHttpBinding);
                var reliableSessionBindingElement = customBinding.Elements.Find<System.ServiceModel.Channels.ReliableSessionBindingElement>();
                reliableSessionBindingElement.ReliableMessagingVersion = clientRmVersion;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(customBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                ClientContract.IEchoService channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                (channel as System.ServiceModel.IClientChannel).Close();
                factory.Close();
                // Create a new ChannelFactory to test connection reuse
                factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(customBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                (channel as System.ServiceModel.IClientChannel).Close();
            }
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public void WSHttpBindingMultipleMessages(ReliableMessagingVersion rmVersion, System.ServiceModel.ReliableMessagingVersion clientRmVersion, bool ordered)
        {
            var startupFilter = new RSStartup(rmVersion, ordered);
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding("WSHttpBinding", System.ServiceModel.SecurityMode.None);
                wsHttpBinding.ReliableSession.Enabled = true;
                wsHttpBinding.ReliableSession.Ordered = ordered;
                System.ServiceModel.Channels.CustomBinding customBinding = new(wsHttpBinding);
                var rsbe = customBinding.Elements.Find<System.ServiceModel.Channels.ReliableSessionBindingElement>();
                rsbe.ReliableMessagingVersion = clientRmVersion;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(customBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                ClientContract.IEchoService channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                for (int i = 0; i < 20; i++)
                {
                    string testString = $"Message {i}: {new string((char)('A' + (i % 26)), 100)}";
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                }
                (channel as System.ServiceModel.IClientChannel).Close();
                factory.Close();
            }
        }

        [Fact]
        public void WSHttpBindingLargeMessage()
        {
            string testString = new('x', 64000);
            var startupFilter = new RSDefaultStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding("WSHttpBinding", System.ServiceModel.SecurityMode.None);
                wsHttpBinding.ReliableSession.Enabled = true;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                ClientContract.IEchoService channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                (channel as System.ServiceModel.IClientChannel).Close();
                factory.Close();
            }
        }

        [Fact]
        public void WSHttpBindingConcurrentSessions()
        {
            var startupFilter = new RSDefaultStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                const int sessionCount = 3;
                var factories = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>[sessionCount];
                var channels = new ClientContract.IEchoService[sessionCount];
                try
                {
                    for (int i = 0; i < sessionCount; i++)
                    {
                        System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding("WSHttpBinding", System.ServiceModel.SecurityMode.None);
                        wsHttpBinding.ReliableSession.Enabled = true;
                        factories[i] = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                            new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                        channels[i] = factories[i].CreateChannel();
                        (channels[i] as System.ServiceModel.IClientChannel).Open();
                    }

                    for (int i = 0; i < sessionCount; i++)
                    {
                        string testString = $"Session {i} message";
                        string result = channels[i].EchoString(testString);
                        Assert.Equal(testString, result);
                    }
                }
                finally
                {
                    for (int i = 0; i < sessionCount; i++)
                    {
                        ServiceHelper.CloseServiceModelObjects(
                            (System.ServiceModel.ICommunicationObject)channels[i], factories[i]);
                    }
                }
            }
        }

        [Fact]
        public void WSHttpBindingAbortSession()
        {
            var startupFilter = new RSDefaultStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding("WSHttpBinding", System.ServiceModel.SecurityMode.None);
                wsHttpBinding.ReliableSession.Enabled = true;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                ClientContract.IEchoService channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                string result = channel.EchoString("test");
                Assert.Equal("test", result);
                // Abort instead of graceful close
                (channel as System.ServiceModel.IClientChannel).Abort();
                factory.Abort();

                // Verify new session works after abort
                factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                result = channel.EchoString("after abort");
                Assert.Equal("after abort", result);
                (channel as System.ServiceModel.IClientChannel).Close();
                factory.Close();
            }
        }

        [Fact]
        public void WS2007HttpBindingReliableSessions()
        {
            var startupFilter = new WS2007RSStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding("WS2007HttpBinding", System.ServiceModel.SecurityMode.None);
                wsHttpBinding.ReliableSession.Enabled = true;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(wsHttpBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                ClientContract.IEchoService channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                string result = channel.EchoString("WS2007 RS test");
                Assert.Equal("WS2007 RS test", result);
                (channel as System.ServiceModel.IClientChannel).Close();
                factory.Close();
            }
        }

        internal class RSStartup : IStartupFilter
        {
            private ReliableMessagingVersion _rmVersion;
            private bool _ordered;
            private const string ServicePath = "/wsHttpReliableSessions.svc";

            public RSStartup(ReliableMessagingVersion rmVersion, bool ordered)
            {
                _rmVersion = rmVersion;
                _ordered = ordered;
            }

            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return builder =>
                {
                    builder.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<Services.EchoService>();
                        var binding = new WSHttpBinding(SecurityMode.None, true);
                        binding.ReliableSession.Ordered = _ordered;
                        var customBinding = new Channels.CustomBinding(binding);
                        var reliableSessionBindingElement = customBinding.Elements.Find<Channels.ReliableSessionBindingElement>();
                        reliableSessionBindingElement.ReliableMessagingVersion = _rmVersion;
                        serviceBuilder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(customBinding, ServicePath);
                    });
                    next(builder);
                };
            }

            public Uri GetServiceUri(IWebHost webHost)
            {
                return new Uri($"http://localhost:{webHost.GetHttpPort()}{ServicePath}");
            }
        }

        internal class RSDefaultStartup : IStartupFilter
        {
            private const string ServicePath = "/wsHttpReliableSessionsDefault.svc";

            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return builder =>
                {
                    builder.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<Services.EchoService>();
                        var binding = new WSHttpBinding(SecurityMode.None, true);
                        serviceBuilder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(binding, ServicePath);
                    });
                    next(builder);
                };
            }

            public Uri GetServiceUri(IWebHost webHost)
            {
                return new Uri($"http://localhost:{webHost.GetHttpPort()}{ServicePath}");
            }
        }

        internal class WS2007RSStartup : IStartupFilter
        {
            private const string ServicePath = "/ws2007HttpReliableSessions.svc";

            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return builder =>
                {
                    builder.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<Services.EchoService>();
                        var binding = new WS2007HttpBinding(SecurityMode.None, true);
                        binding.ReliableSession.Ordered = true;
                        serviceBuilder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(binding, ServicePath);
                    });
                    next(builder);
                };
            }

            public Uri GetServiceUri(IWebHost webHost)
            {
                return new Uri($"http://localhost:{webHost.GetHttpPort()}{ServicePath}");
            }
        }

        public static IEnumerable<object[]> GetTestVariations()
        {
            yield return new object[] { ReliableMessagingVersion.WSReliableMessaging11, System.ServiceModel.ReliableMessagingVersion.WSReliableMessaging11, true };
            yield return new object[] { ReliableMessagingVersion.WSReliableMessaging11, System.ServiceModel.ReliableMessagingVersion.WSReliableMessaging11, false };
            yield return new object[] { ReliableMessagingVersion.WSReliableMessagingFebruary2005, System.ServiceModel.ReliableMessagingVersion.WSReliableMessagingFebruary2005, true };
            yield return new object[] { ReliableMessagingVersion.WSReliableMessagingFebruary2005, System.ServiceModel.ReliableMessagingVersion.WSReliableMessagingFebruary2005, false };
        }
    }
}
