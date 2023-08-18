// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using Xunit.Abstractions;

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
                System.ServiceModel.WSHttpBinding wsHttpBinding = ClientHelper.GetBufferedModeWSHttpBinding(System.ServiceModel.SecurityMode.None);
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
                // Create a new ChannelFactory to force a new HTTP connection pool
                factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(customBinding,
                    new System.ServiceModel.EndpointAddress(startupFilter.GetServiceUri(host)));
                channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                (channel as System.ServiceModel.IClientChannel).Close();
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
                    builder.Use(next =>
                    {
                        return context =>
                        {
                            Console.WriteLine("Content length: " + context.Request.Headers.ContentLength);
                            return next(context);
                        };
                    });
                    builder.UseServiceModel(builder =>
                    {
                        builder.AddService<Services.EchoService>();
                        var binding = new WSHttpBinding(SecurityMode.None, true);
                        binding.ReliableSession.Ordered = _ordered;
                        var customBinding = new Channels.CustomBinding(binding);
                        var reliableSessionBindingElement = customBinding.Elements.Find<Channels.ReliableSessionBindingElement>();
                        reliableSessionBindingElement.ReliableMessagingVersion = _rmVersion;
                        builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(customBinding, ServicePath);
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
