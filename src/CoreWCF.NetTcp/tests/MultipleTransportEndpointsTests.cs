// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using System.Text;
using System.Xml;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.NetTcp.Tests
{
    // Regression test for https://github.com/CoreWCF/CoreWCF/issues/1742
    // Hosting two or more net.tcp endpoints with distinct addresses (and therefore
    // distinct ListenUris) under SecurityMode.Transport on a single service used to
    // throw "Identity can only be set once." at host startup on 1.9.x because the
    // per-connection identity was fanned out over every ChannelDispatcher on the host.
    public class MultipleTransportEndpointsTests
    {
        private readonly ITestOutputHelper _output;

        public MultipleTransportEndpointsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WindowsOnlyFact]
        public void HostStartsWithMultipleTransportSecurityEndpoints()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                // Before the fix this threw System.ArgumentException: "Identity can only be set once."
                host.Start();

                System.ServiceModel.ChannelFactory<ClientContract.ITestService> firstFactory = null;
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> secondFactory = null;
                ClientContract.ITestService firstChannel = null;
                ClientContract.ITestService secondChannel = null;
                try
                {
                    System.ServiceModel.NetTcpBinding binding =
                        ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.Transport);

                    firstFactory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.FirstRelativeAddress));
                    firstChannel = firstFactory.CreateChannel();
                    ((IChannel)firstChannel).Open();
                    Assert.Equal("first", firstChannel.EchoString("first"));
                    ((IChannel)firstChannel).Close();

                    secondFactory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.SecondRelativeAddress));
                    secondChannel = secondFactory.CreateChannel();
                    ((IChannel)secondChannel).Open();
                    Assert.Equal("second", secondChannel.EchoString("second"));
                    ((IChannel)secondChannel).Close();

                    firstFactory.Close();
                    secondFactory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)firstChannel, firstFactory, (IChannel)secondChannel, secondFactory);
                }
            }
        }

        // Each net.tcp endpoint is given a distinct explicit UpnEndpointIdentity. This verifies that
        // hosting multiple Transport-security endpoints no longer throws at startup AND that each
        // endpoint keeps its own identity rather than having a single identity fanned out over all of
        // them (which is what regressed in 1.9.x).
        [WindowsOnlyFact]
        public void EachEndpointRetainsItsOwnExplicitIdentity()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<ExplicitIdentityStartup>(_output).Build();
            using (host)
            {
                // Before the fix this threw System.ArgumentException: "Identity can only be set once."
                host.Start();

                ServiceHostCapture capture = host.Services.GetRequiredService<ServiceHostCapture>();
                (EndpointAddress first, EndpointAddress second) = GetEndpointAddresses(
                    capture.ServiceHost, Startup.FirstRelativeAddress, Startup.SecondRelativeAddress);

                string firstXml = SerializeAddress(first);
                string secondXml = SerializeAddress(second);

                // Each endpoint should publish its own identity and only its own.
                Assert.Contains(ExplicitIdentityStartup.FirstUpn, firstXml);
                Assert.Contains(ExplicitIdentityStartup.SecondUpn, secondXml);
                Assert.DoesNotContain(ExplicitIdentityStartup.SecondUpn, firstXml);
                Assert.DoesNotContain(ExplicitIdentityStartup.FirstUpn, secondXml);
            }
        }

        // With no explicit identity set, the transport-derived (Windows) identity should still be applied
        // to every endpoint. This guards against the fix accidentally becoming a no-op (never matching an
        // endpoint and therefore leaving the identity unset).
        [WindowsOnlyFact]
        public void EachEndpointGetsTransportIdentityApplied()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<TransportIdentityStartup>(_output).Build();
            using (host)
            {
                host.Start();

                ServiceHostCapture capture = host.Services.GetRequiredService<ServiceHostCapture>();
                (EndpointAddress first, EndpointAddress second) = GetEndpointAddresses(
                    capture.ServiceHost, Startup.FirstRelativeAddress, Startup.SecondRelativeAddress);

                Assert.NotNull(first.Identity);
                Assert.NotNull(second.Identity);
            }
        }

        private static (EndpointAddress First, EndpointAddress Second) GetEndpointAddresses(
            ServiceHostBase serviceHost, string firstPath, string secondPath)
        {
            EndpointAddress first = null;
            EndpointAddress second = null;
            bool firstFound = false;
            bool secondFound = false;

            foreach (ChannelDispatcherBase channelDispatcherBase in serviceHost.ChannelDispatchers)
            {
                if (!(channelDispatcherBase is ChannelDispatcher channelDispatcher))
                {
                    continue;
                }

                foreach (EndpointDispatcher endpointDispatcher in channelDispatcher.Endpoints)
                {
                    EndpointAddress address = endpointDispatcher.EndpointAddress;
                    if (address?.Uri == null)
                    {
                        // Null address means this isn't an endpoint we can match against.
                        continue;
                    }

                    string path = address.Uri.AbsolutePath;
                    if (path.EndsWith(firstPath, StringComparison.OrdinalIgnoreCase))
                    {
                        first = address;
                        firstFound = true;
                    }
                    else if (path.EndsWith(secondPath, StringComparison.OrdinalIgnoreCase))
                    {
                        second = address;
                        secondFound = true;
                    }
                }
            }

            Assert.True(firstFound, $"Did not find an EndpointDispatcher for {firstPath}.");
            Assert.True(secondFound, $"Did not find an EndpointDispatcher for {secondPath}.");
            return (first, second);
        }

        private static string SerializeAddress(EndpointAddress address)
        {
            var builder = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(builder))
            {
                address.WriteTo(Channels.AddressingVersion.WSAddressing10, writer);
            }

            return builder.ToString();
        }

        internal class Startup
        {
            public const string FirstRelativeAddress = "/nettcp.svc/a";
            public const string SecondRelativeAddress = "/nettcp.svc/b";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    // Two net.tcp endpoints on a single service with distinct addresses,
                    // both using Transport security (the default for NetTcpBinding).
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new NetTcpBinding(SecurityMode.Transport), FirstRelativeAddress);
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new NetTcpBinding(SecurityMode.Transport), SecondRelativeAddress);
                });
            }
        }

        internal sealed class ServiceHostCapture
        {
            public ServiceHostBase ServiceHost { get; set; }
        }

        // Hosts the same two Transport-security endpoints as Startup but assigns each a distinct
        // explicit UpnEndpointIdentity and captures the ServiceHostBase so the test can inspect the
        // resulting per-endpoint dispatcher state.
        internal class ExplicitIdentityStartup
        {
            public const string FirstUpn = "first@corewcf.net";
            public const string SecondUpn = "second@corewcf.net";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton<ServiceHostCapture>();
            }

            public void Configure(IApplicationBuilder app)
            {
                ServiceHostCapture capture = app.ApplicationServices.GetRequiredService<ServiceHostCapture>();
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new NetTcpBinding(SecurityMode.Transport), Startup.FirstRelativeAddress);
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new NetTcpBinding(SecurityMode.Transport), Startup.SecondRelativeAddress);
                    builder.ConfigureServiceHostBase<Services.TestService>(serviceHost =>
                    {
                        capture.ServiceHost = serviceHost;
                        foreach (Description.ServiceEndpoint endpoint in serviceHost.Description.Endpoints)
                        {
                            string path = endpoint.Address.Uri.AbsolutePath;
                            if (path.EndsWith(Startup.FirstRelativeAddress, StringComparison.OrdinalIgnoreCase))
                            {
                                endpoint.Address = new EndpointAddress(endpoint.Address.Uri, new UpnEndpointIdentity(FirstUpn));
                            }
                            else if (path.EndsWith(Startup.SecondRelativeAddress, StringComparison.OrdinalIgnoreCase))
                            {
                                endpoint.Address = new EndpointAddress(endpoint.Address.Uri, new UpnEndpointIdentity(SecondUpn));
                            }
                        }
                    });
                });
            }
        }

        // Hosts the same two Transport-security endpoints and captures the ServiceHostBase, but leaves
        // the transport-derived identity in place (no explicit UpnEndpointIdentity).
        internal class TransportIdentityStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton<ServiceHostCapture>();
            }

            public void Configure(IApplicationBuilder app)
            {
                ServiceHostCapture capture = app.ApplicationServices.GetRequiredService<ServiceHostCapture>();
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new NetTcpBinding(SecurityMode.Transport), Startup.FirstRelativeAddress);
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new NetTcpBinding(SecurityMode.Transport), Startup.SecondRelativeAddress);
                    builder.ConfigureServiceHostBase<Services.TestService>(serviceHost =>
                    {
                        capture.ServiceHost = serviceHost;
                    });
                });
            }
        }
    }
}
