// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace CoreWCF.NetTcp
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
        public void NetTcpBindingSimpleReliableSessions(ReliableMessagingVersion rmVersion, System.ServiceModel.ReliableMessagingVersion clientRmVersion, bool ordered)
        {
            string testString = new('a', 3000);
            var startupFilter = new RSStartup(rmVersion, ordered);
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding netTcpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                netTcpBinding.ReliableSession.Enabled = true;
                netTcpBinding.ReliableSession.Ordered = ordered;
                System.ServiceModel.Channels.CustomBinding customBinding = new(netTcpBinding);
                var reliableSessionBindingElement = customBinding.Elements.Find<System.ServiceModel.Channels.ReliableSessionBindingElement>();
                reliableSessionBindingElement.ReliableMessagingVersion = clientRmVersion;
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(customBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                Contract.IEchoService channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                (channel as System.ServiceModel.IClientChannel).Close();
                factory.Close();
                // Create a new ChannelFactory to test connection reuse
                factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(customBinding,
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
        public void NetTcpBindingMultipleMessages(ReliableMessagingVersion rmVersion, System.ServiceModel.ReliableMessagingVersion clientRmVersion, bool ordered)
        {
            var startupFilter = new RSStartup(rmVersion, ordered);
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding netTcpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                netTcpBinding.ReliableSession.Enabled = true;
                netTcpBinding.ReliableSession.Ordered = ordered;
                System.ServiceModel.Channels.CustomBinding customBinding = new(netTcpBinding);
                var rsbe = customBinding.Elements.Find<System.ServiceModel.Channels.ReliableSessionBindingElement>();
                rsbe.ReliableMessagingVersion = clientRmVersion;
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(customBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                Contract.IEchoService channel = factory.CreateChannel();
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
        public void NetTcpBindingLargeMessage()
        {
            string testString = new('x', 64000);
            var startupFilter = new RSDefaultStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding netTcpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                netTcpBinding.ReliableSession.Enabled = true;
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(netTcpBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                Contract.IEchoService channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                (channel as System.ServiceModel.IClientChannel).Close();
                factory.Close();
            }
        }

        [Fact]
        public void NetTcpBindingConcurrentSessions()
        {
            var startupFilter = new RSDefaultStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                const int sessionCount = 3;
                var factories = new System.ServiceModel.ChannelFactory<Contract.IEchoService>[sessionCount];
                var channels = new Contract.IEchoService[sessionCount];
                try
                {
                    for (int i = 0; i < sessionCount; i++)
                    {
                        System.ServiceModel.NetTcpBinding netTcpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                        netTcpBinding.ReliableSession.Enabled = true;
                        factories[i] = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(netTcpBinding,
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
        public void NetTcpBindingAbortSession()
        {
            var startupFilter = new RSDefaultStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding netTcpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                netTcpBinding.ReliableSession.Enabled = true;
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(netTcpBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                Contract.IEchoService channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                string result = channel.EchoString("test");
                Assert.Equal("test", result);
                // Abort instead of graceful close
                (channel as System.ServiceModel.IClientChannel).Abort();
                factory.Abort();

                // Verify new session works after abort
                factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(netTcpBinding,
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
        public void NetTcpBindingDefaultReliableSessionBinding()
        {
            // Test using NetTcpBinding with reliableSessionEnabled=true in constructor (no CustomBinding)
            var startupFilter = new RSDefaultStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startupFilter, _output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding netTcpBinding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                netTcpBinding.ReliableSession.Enabled = true;
                netTcpBinding.ReliableSession.Ordered = true;
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(netTcpBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                Contract.IEchoService channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                string result = channel.EchoString("default binding test");
                Assert.Equal("default binding test", result);
                (channel as System.ServiceModel.IClientChannel).Close();
                factory.Close();
            }
        }

        internal class RSStartup : IStartupFilter
        {
            private ReliableMessagingVersion _rmVersion;
            private bool _ordered;
            private const string ServicePath = "/netTcpReliableSessions.svc";

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
                        var binding = new NetTcpBinding(SecurityMode.None, true);
                        binding.ReliableSession.Ordered = _ordered;
                        var customBinding = new Channels.CustomBinding(binding);
                        var reliableSessionBindingElement = customBinding.Elements.Find<Channels.ReliableSessionBindingElement>();
                        reliableSessionBindingElement.ReliableMessagingVersion = _rmVersion;
                        serviceBuilder.AddServiceEndpoint<Services.EchoService, Contract.IEchoService>(customBinding, ServicePath);
                    });
                    next(builder);
                };
            }

            public Uri GetServiceUri(IWebHost webHost)
            {
                return new Uri($"{webHost.GetNetTcpAddressInUse()}{ServicePath}");
            }
        }

        internal class RSDefaultStartup : IStartupFilter
        {
            private const string ServicePath = "/netTcpReliableSessionsDefault.svc";

            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return builder =>
                {
                    builder.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<Services.EchoService>();
                        var binding = new NetTcpBinding(SecurityMode.None, true);
                        serviceBuilder.AddServiceEndpoint<Services.EchoService, Contract.IEchoService>(binding, ServicePath);
                    });
                    next(builder);
                };
            }

            public Uri GetServiceUri(IWebHost webHost)
            {
                return new Uri($"{webHost.GetNetTcpAddressInUse()}{ServicePath}");
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
