// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace CoreWCF.Http.Tests
{
    // HTTP-side coverage tests for Reliable Sessions. HTTP RS uses the IReplyChannel /
    // request-reply binder path (ServerReliableChannelBinder<IReplyChannel> +
    // ReliableInputSessionChannel/ReliableReplySessionChannel), which is a different code
    // path than the IDuplexSessionChannel path tested in the NetTcp suite. Adding HTTP
    // tests here exercises the request-reply binder and HTTP-specific transport flow.
    public class ReliableSessionsCoverageTests
    {
        private readonly ITestOutputHelper _output;

        public ReliableSessionsCoverageTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // Server-side inactivity timeout over HTTP request-reply transport. Drives the
        // ChannelReliableSession.OnInactivityElapsed -> SequenceTerminatedFault path
        // through the HTTP server binder (ServerReliableChannelBinder<IReplyChannel>).
        [Fact]
        public void Http_InactivityTimeout_FaultsSession()
        {
            var startup = new ConfigurableHttpRsStartup(rsbe =>
            {
                rsbe.InactivityTimeout = TimeSpan.FromSeconds(3);
            });
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.WSHttpBinding binding = ClientHelper.GetBufferedModeWSHttpBinding(
                    "WSHttpBinding", System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                binding.ReliableSession.InactivityTimeout = TimeSpan.FromMinutes(10);
                binding.SendTimeout = TimeSpan.FromSeconds(20);
                binding.ReceiveTimeout = TimeSpan.FromMinutes(2);
                var endpoint = new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host));
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(binding, endpoint);
                ClientContract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();
                    Assert.Equal("hello", channel.EchoString("hello"));

                    // Wait > server InactivityTimeout. Server's inactivity timer fires and
                    // terminates the sequence.
                    Thread.Sleep(TimeSpan.FromSeconds(6));

                    Exception caught = Record.Exception(() => channel.EchoString("after-timeout"));
                    Assert.NotNull(caught);
                    _output.WriteLine($"Post-inactivity exception: {caught.GetType().FullName}: {caught.Message}");
                    Assert.True(
                        caught is System.ServiceModel.CommunicationException ||
                        caught is System.ServiceModel.CommunicationObjectFaultedException ||
                        caught is TimeoutException ||
                        caught is InvalidOperationException,
                        $"Expected fault/communication exception, got {caught.GetType().FullName}");
                }
                finally
                {
                    SafeAbort(channel, factory);
                }
            }
        }

        // Drives operation-fault dispatch on the HTTP RS reply path -- the service throws,
        // the dispatcher must produce a SOAP fault that flows back through
        // ReliableReplySessionChannel.
        [Fact]
        public void Http_ServiceOperationFaults_PropagateAsFaultException()
        {
            var startup = new HttpFaultingStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.WSHttpBinding binding = ClientHelper.GetBufferedModeWSHttpBinding(
                    "WSHttpBinding", System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(binding,
                    new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host)));
                ClientContract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();

                    Exception caught = Record.Exception(() => channel.EchoString("fault-please"));
                    Assert.NotNull(caught);
                    _output.WriteLine($"Operation-fault exception: {caught.GetType().FullName}: {caught.Message}");
                    Assert.True(
                        caught is System.ServiceModel.FaultException ||
                        caught is System.ServiceModel.CommunicationException,
                        $"Expected fault, got {caught.GetType().FullName}");
                }
                finally
                {
                    SafeAbort(channel, factory);
                }
            }
        }

        // Concurrent calls on a single HTTP reliable session. Exercises window/ordering
        // management for the IReplyChannel path.
        [Fact]
        public async Task Http_ConcurrentCalls_OnSingleSession_AllSucceed()
        {
            var startup = new ConfigurableHttpRsStartup(rsbe =>
            {
                rsbe.MaxTransferWindowSize = 4;
            });
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.WSHttpBinding binding = ClientHelper.GetBufferedModeWSHttpBinding(
                    "WSHttpBinding", System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                binding.ReliableSession.Ordered = false;
                binding.SendTimeout = TimeSpan.FromMinutes(1);
                var customBinding = new System.ServiceModel.Channels.CustomBinding(binding);
                var rsbe = customBinding.Elements.Find<System.ServiceModel.Channels.ReliableSessionBindingElement>();
                rsbe.MaxTransferWindowSize = 4;

                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(customBinding,
                    new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host)));
                ClientContract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();

                    const int parallel = 12;
                    var tasks = new Task<string>[parallel];
                    for (int i = 0; i < parallel; i++)
                    {
                        int idx = i;
                        tasks[idx] = Task.Run(() => channel.EchoString($"msg-{idx}"));
                    }
                    Task all = Task.WhenAll(tasks);
                    Task completed = await Task.WhenAny(all, Task.Delay(TimeSpan.FromMinutes(1)));
                    Assert.Same(all, completed);
                    await all;
                    for (int i = 0; i < parallel; i++)
                    {
                        Assert.Equal($"msg-{i}", await tasks[i]);
                    }
                }
                finally
                {
                    SafeAbort(channel, factory);
                }
            }
        }

        // Exercises the binding option setters/getters on the HTTP path with non-default
        // values. Each variation forces ChannelReliableSession to initialize differently.
        [Theory]
        [InlineData(/*window*/ 1,  /*ordered*/ true,  /*flow*/ true,  /*ackMs*/ 200, /*inactivitySec*/ 30)]
        [InlineData(/*window*/ 16, /*ordered*/ false, /*flow*/ false, /*ackMs*/ 25,  /*inactivitySec*/ 60)]
        public void Http_BindingConfiguration_Variations(int window, bool ordered, bool flow, int ackMs, int inactivitySec)
        {
            var startup = new ConfigurableHttpRsStartup(rsbe =>
            {
                rsbe.MaxTransferWindowSize = window;
                rsbe.Ordered = ordered;
                rsbe.FlowControlEnabled = flow;
                rsbe.AcknowledgementInterval = TimeSpan.FromMilliseconds(ackMs);
                rsbe.InactivityTimeout = TimeSpan.FromSeconds(inactivitySec);
            });
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.WSHttpBinding binding = ClientHelper.GetBufferedModeWSHttpBinding(
                    "WSHttpBinding", System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                binding.ReliableSession.Ordered = ordered;
                binding.ReliableSession.InactivityTimeout = TimeSpan.FromSeconds(inactivitySec);
                var customBinding = new System.ServiceModel.Channels.CustomBinding(binding);
                var clientRsbe = customBinding.Elements.Find<System.ServiceModel.Channels.ReliableSessionBindingElement>();
                clientRsbe.MaxTransferWindowSize = window;
                clientRsbe.FlowControlEnabled = flow;
                clientRsbe.AcknowledgementInterval = TimeSpan.FromMilliseconds(ackMs);

                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(customBinding,
                    new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host)));
                ClientContract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();
                    for (int i = 0; i < 5; i++)
                    {
                        Assert.Equal($"x{i}", channel.EchoString($"x{i}"));
                    }
                    (channel as System.ServiceModel.IClientChannel).Close();
                }
                finally
                {
                    if ((channel as System.ServiceModel.IClientChannel).State != System.ServiceModel.CommunicationState.Closed)
                    {
                        SafeAbort(channel, factory);
                    }
                    else
                    {
                        try { factory.Close(); } catch { factory.Abort(); }
                    }
                }
            }
        }

        // Client abort on an active HTTP reliable session. Exercises HTTP transport
        // disconnect-during-session paths and server-side cleanup.
        [Fact]
        public void Http_ClientAbort_DoesNotHangServer()
        {
            var startup = new ConfigurableHttpRsStartup(rsbe => { });
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.WSHttpBinding binding = ClientHelper.GetBufferedModeWSHttpBinding(
                    "WSHttpBinding", System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(binding,
                    new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host)));
                ClientContract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();
                    Assert.Equal("first", channel.EchoString("first"));

                    (channel as System.ServiceModel.IClientChannel).Abort();
                    factory.Abort();

                    var factory2 = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(binding,
                        new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host)));
                    ClientContract.IEchoService channel2 = factory2.CreateChannel();
                    try
                    {
                        (channel2 as System.ServiceModel.IClientChannel).Open();
                        Assert.Equal("second", channel2.EchoString("second"));
                    }
                    finally
                    {
                        SafeAbort(channel2, factory2);
                    }
                }
                finally
                {
                    try { factory.Abort(); } catch { }
                }
            }
        }

        // ----------------------- helpers -----------------------

        private static void SafeAbort(object channel, System.ServiceModel.ICommunicationObject factory)
        {
            try { (channel as System.ServiceModel.IClientChannel)?.Abort(); } catch { }
            try { factory?.Abort(); } catch { }
        }

        // ----------------------- contracts / services -----------------------

        [CoreWCF.ServiceBehavior(IncludeExceptionDetailInFaults = true)]
        internal sealed class HttpFaultingEchoService : ServiceContract.IEchoService
        {
            public string EchoString(string echo)
            {
                throw new InvalidOperationException("intentional service-side failure for HTTP coverage test");
            }

            public System.IO.Stream EchoStream(System.IO.Stream s) => throw new NotSupportedException();
            public Task<string> EchoStringAsync(string echo) => throw new NotSupportedException();
            public Task<System.IO.Stream> EchoStreamAsync(System.IO.Stream stream) => throw new NotSupportedException();
            public string EchoToFail(string echo) => throw new NotSupportedException();
            public string EchoForImpersonation(string echo) => throw new NotSupportedException();
        }

        // ----------------------- startups -----------------------

        internal sealed class ConfigurableHttpRsStartup : IStartupFilter
        {
            private const string PathBase = "/rsHttpCoverage";
            private static int s_pathCounter;
            private readonly string _path;
            private readonly Action<Channels.ReliableSessionBindingElement> _configure;

            public ConfigurableHttpRsStartup(Action<Channels.ReliableSessionBindingElement> configure)
            {
                _configure = configure;
                _path = $"{PathBase}-{Interlocked.Increment(ref s_pathCounter)}.svc";
            }

            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                builder =>
                {
                    builder.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<Services.EchoService>();
                        var ws = new WSHttpBinding(SecurityMode.None, true);
                        var custom = new Channels.CustomBinding(ws);
                        var rsbe = custom.Elements.Find<Channels.ReliableSessionBindingElement>();
                        _configure(rsbe);
                        serviceBuilder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(custom, _path);
                    });
                    next(builder);
                };

            public Uri GetServiceUri(IWebHost host) =>
                new Uri($"http://localhost:{host.GetHttpPort()}{_path}");
        }

        internal sealed class HttpFaultingStartup : IStartupFilter
        {
            private const string Path = "/rsHttpFaulting.svc";

            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                builder =>
                {
                    builder.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<HttpFaultingEchoService>(opt =>
                        {
                            opt.DebugBehavior.IncludeExceptionDetailInFaults = true;
                        });
                        var ws = new WSHttpBinding(SecurityMode.None, true);
                        serviceBuilder.AddServiceEndpoint<HttpFaultingEchoService, ServiceContract.IEchoService>(ws, Path);
                    });
                    next(builder);
                };

            public Uri GetServiceUri(IWebHost host) =>
                new Uri($"http://localhost:{host.GetHttpPort()}{Path}");
        }
    }
}
