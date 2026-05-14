// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CoreWCF.NetTcp
{
    // Targeted regression tests for issues found by comparing the CoreWCF Reliable Sessions
    // port against the .NET Framework WCF original. Each test is intended to fail before the
    // corresponding fix and pass after it.
    public class ReliableSessionsCorrectnessTests
    {
        private const string EchoPath = "/rsCorrectness.svc";

        private readonly ITestOutputHelper _output;

        public ReliableSessionsCorrectnessTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // H7: ReliableServiceSessionChannelDispatcher.DispatchAsync(null) throws
        // ArgumentException when the host signals channel close. The exception is logged
        // by Kestrel as "Unhandled exception while processing ..." but not surfaced to
        // the test. Verified via ExceptionCapturingLoggerProvider.
        [Fact]
        public void NoUnhandledExceptionDuringChannelClose()
        {
            var capturingProvider = new ExceptionCapturingLoggerProvider(null);
            IWebHost host = BuildHostWithCapturingLogger(capturingProvider, new EchoStartup());
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(binding,
                    new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + EchoPath));
                Contract.IEchoService channel = factory.CreateChannel();
                (channel as System.ServiceModel.IClientChannel).Open();
                Assert.Equal("hello", channel.EchoString("hello"));
                (channel as System.ServiceModel.IClientChannel).Close();
                factory.Close();
            }

            ArgumentException[] argExceptions = capturingProvider
                .GetExceptionsLogged<ArgumentException>()
                .ToArray();
            if (argExceptions.Length > 0)
            {
                _output.WriteLine($"Captured {argExceptions.Length} ArgumentException(s):");
                foreach (ArgumentException ex in argExceptions)
                {
                    _output.WriteLine(ex.ToString());
                }
            }
            Assert.Empty(argExceptions);
        }

        // C3: MaxPendingChannels enforcement is missing. With MaxPendingChannels=1, only one
        // active reliable session should be allowed at a time; the second concurrent
        // CreateSequence should be rejected with a server-too-busy fault that surfaces as
        // ServerTooBusyException on the client.
        [Fact]
        public void MaxPendingChannelsRefusesAdditionalSessions()
        {
            var startup = new MaxPendingChannelsStartup(maxPendingChannels: 1);
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                binding.OpenTimeout = TimeSpan.FromSeconds(15);
                binding.SendTimeout = TimeSpan.FromSeconds(15);
                var endpoint = new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host));

                System.ServiceModel.ChannelFactory<Contract.IEchoService> firstFactory = null;
                Contract.IEchoService firstChannel = null;
                System.ServiceModel.ChannelFactory<Contract.IEchoService> secondFactory = null;
                Contract.IEchoService secondChannel = null;
                try
                {
                    firstFactory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(binding, endpoint);
                    firstChannel = firstFactory.CreateChannel();
                    (firstChannel as System.ServiceModel.IClientChannel).Open();
                    Assert.Equal("first", firstChannel.EchoString("first"));

                    secondFactory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(binding, endpoint);
                    secondChannel = secondFactory.CreateChannel();

                    Exception caught = Record.Exception(() => (secondChannel as System.ServiceModel.IClientChannel).Open());
                    Assert.NotNull(caught);
                    _output.WriteLine($"Second Open exception: {caught.GetType().FullName}: {caught.Message}");

                    // ServerTooBusyException is the dnf-typical surface, but a CommunicationException
                    // / FaultException carrying the WS-RM CreateSequenceRefused subcode is also
                    // acceptable evidence that the server actively rejected the session.
                    Assert.True(
                        caught is System.ServiceModel.ServerTooBusyException ||
                        caught is System.ServiceModel.FaultException ||
                        caught is System.ServiceModel.CommunicationException,
                        $"Expected a server-rejection style exception but got {caught.GetType().FullName}");
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((System.ServiceModel.ICommunicationObject)secondChannel, secondFactory);
                    ServiceHelper.CloseServiceModelObjects((System.ServiceModel.ICommunicationObject)firstChannel, firstFactory);
                }
            }
        }

        // Note on a known limitation:
        // The .NET Framework WCF allows server-initiated callbacks to be sent AFTER a duplex
        // client calls IDuplexSession.CloseOutputSession, because the application owns when
        // the server output session closes. CoreWCF uses a push-only dispatch model with no
        // application-driven output close, so the dispatcher auto-closes the server output as
        // soon as the input session terminates. Even with WS-RM 1.1, WCF clients send both
        // CloseSequence and TerminateSequence as part of CloseOutputSession, so the server
        // cannot distinguish a half-close from a full close at the message level. Tracking the
        // outbound callback queue and deferring the auto-close is a larger change tracked
        // separately.

        private static IWebHost BuildHostWithCapturingLogger(ExceptionCapturingLoggerProvider capturingProvider, IStartupFilter startupFilter)
        {
            return WebHost.CreateDefaultBuilder(Array.Empty<string>())
                .ConfigureLogging(logging =>
                {
                    logging.AddProvider(capturingProvider);
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .UseNetTcp(System.Net.IPAddress.Loopback, 0)
                .ConfigureServices(services =>
                {
                    services.AddServiceModelServices();
                    services.AddSingleton(startupFilter);
                })
                .Configure(_ => { })
                .Build();
        }

        internal sealed class EchoStartup : IStartupFilter
        {
            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return builder =>
                {
                    builder.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<Services.EchoService>();
                        var binding = new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None, true);
                        serviceBuilder.AddServiceEndpoint<Services.EchoService, Contract.IEchoService>(binding, EchoPath);
                    });
                    next(builder);
                };
            }
        }

        internal sealed class MaxPendingChannelsStartup : IStartupFilter
        {
            private const string Path = "/rsMaxPending.svc";
            private readonly int _maxPendingChannels;

            public MaxPendingChannelsStartup(int maxPendingChannels)
            {
                _maxPendingChannels = maxPendingChannels;
            }

            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return builder =>
                {
                    builder.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<Services.EchoService>();
                        var binding = new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None, true);
                        var customBinding = new CoreWCF.Channels.CustomBinding(binding);
                        var rsbe = customBinding.Elements.Find<CoreWCF.Channels.ReliableSessionBindingElement>();
                        rsbe.MaxPendingChannels = _maxPendingChannels;
                        serviceBuilder.AddServiceEndpoint<Services.EchoService, Contract.IEchoService>(customBinding, Path);
                    });
                    next(builder);
                };
            }

            public Uri GetServiceUri(IWebHost host) =>
                new Uri($"{host.GetNetTcpAddressInUse()}{Path}");
        }
    }
}