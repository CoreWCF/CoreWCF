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

        // C4 + H1: After a duplex client calls IDuplexSession.CloseOutputSession in WSRM 1.1,
        // the server must NOT auto-close its server-to-client output sequence on receipt of
        // CloseSequence. Otherwise pending server-initiated callbacks fail with
        // "Send cannot be called after CloseOutputSession". The server should only auto-close
        // its output once the client sends TerminateSequence, which signals a full close.
        //
        // The .NET Framework WCF client sends both CloseSequence and TerminateSequence as part
        // of CloseOutputSession (it does not implement a true half-close), so this scenario is
        // not reachable with a stock client. We therefore use a test interceptor that strips
        // outbound TerminateSequence from the client side, simulating a well-behaved peer that
        // genuinely wants a half-close.
        [NetCoreOnlyFact]
        public void Wsrm11_HalfCloseAllowsServerCallback()
        {
            CallbackTriggerService.Reset();
            var startup = new CallbackTriggerStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.NetTcpBinding netTcp = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                netTcp.ReliableSession.Enabled = true;
                System.ServiceModel.Channels.CustomBinding clientBinding = new System.ServiceModel.Channels.CustomBinding(netTcp);
                System.ServiceModel.Channels.ReliableSessionBindingElement rsbe =
                    clientBinding.Elements.Find<System.ServiceModel.Channels.ReliableSessionBindingElement>();
                rsbe.ReliableMessagingVersion = System.ServiceModel.ReliableMessagingVersion.WSReliableMessaging11;

                var interceptor = new Helpers.Interceptor.WsrmHalfCloseInterceptor();
                int transportIndex = clientBinding.Elements.Count - 1;
                clientBinding.Elements.Insert(transportIndex, new Helpers.Interceptor.InterceptingBindingElement(interceptor));

                var endpoint = new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host));
                var callback = new CallbackHandler();
                var factory = new System.ServiceModel.DuplexChannelFactory<ITriggerService>(
                    new System.ServiceModel.InstanceContext(callback), clientBinding, endpoint);
                ITriggerService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();

                    channel.TriggerBackgroundCallback("payload");

                    System.ServiceModel.Channels.IDuplexSession session =
                        (channel as System.ServiceModel.IClientChannel)
                            .GetProperty<System.ServiceModel.Channels.IDuplexSessionChannel>()?.Session;
                    Assert.NotNull(session);

                    // Tell the interceptor to drop client-side TerminateSequence so the server
                    // sees CloseSequence but not TerminateSequence. The CoreWCF client will
                    // otherwise send both as part of CloseOutputSession.
                    interceptor.SuppressOutboundTerminateSequence = true;

                    // Best-effort half-close. The client's TerminateSequenceResponse will time
                    // out (we suppressed the request), so guard it with a short timeout and
                    // continue regardless. The point is to send CloseSequence on the wire.
                    try { session.CloseOutputSession(TimeSpan.FromSeconds(2)); }
                    catch { /* expected: server never sees TerminateSequence so the wait may time out */ }

                    Thread.Sleep(500);

                    CallbackTriggerService.ReleaseGate();

                    bool received = callback.Received.WaitOne(TimeSpan.FromSeconds(10));
                    Exception serverSideFailure = CallbackTriggerService.LastError;
                    if (serverSideFailure != null)
                    {
                        _output.WriteLine("Server-side callback exception:");
                        _output.WriteLine(serverSideFailure.ToString());
                    }
                    Assert.Null(serverSideFailure);
                    Assert.True(received, "Client did not receive the expected callback after half-close.");
                }
                finally
                {
                    interceptor.SuppressOutboundTerminateSequence = false;
                    try { (channel as System.ServiceModel.IClientChannel).Abort(); }
                    catch { }
                    try { factory.Abort(); } catch { }
                }
            }
        }

        // H22: when the client cleanly terminates a one-way reliable session
        // (CloseSequence + TerminateSequence in WSRM 1.1, or LastMessage + TerminateSequence
        // in Feb2005), the server-side ReliableInputSessionChannel must observe scheduleShutdown
        // and asynchronously close itself. CoreWCF previously set scheduleShutdown=true but
        // never acted on it -- the channel stayed in Opened state indefinitely, leaking the
        // per-channel DI scope and holding the binder open until transport timeout.
        //
        // dnf: ActionItem.Schedule(this.ShutdownCallback, null) at
        //   ReliableInputSessionChannel.cs:886 (OverDuplex) and 1257 (OverReply).
        //
        // Note: today CoreWCF.Channels.ReliableSessionBindingElement.BuildServiceDispatcher
        // throws PlatformNotSupportedException for IInputSessionChannel listeners (because
        // no IInputSessionChannel-shaped reliable transport binding is currently shipped in
        // CoreWCF), so this code path is not reachable from a production binding. The fix is
        // still applied defensively because the bug would surface immediately if/when
        // IInputSessionChannel support is added (otherwise every one-way reliable session
        // would leak its DI scope on clean termination). Driving this path through an
        // integration test will require IInputSessionChannel support to be wired up first.

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

        internal sealed class CallbackTriggerStartup : IStartupFilter
        {
            private const string Path = "/rsCallbackTrigger.svc";

            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
            {
                return builder =>
                {
                    builder.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<CallbackTriggerService>();
                        var netTcp = new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None, true);
                        var binding = new CoreWCF.Channels.CustomBinding(netTcp);
                        var rsbe = binding.Elements.Find<CoreWCF.Channels.ReliableSessionBindingElement>();
                        rsbe.ReliableMessagingVersion = CoreWCF.ReliableMessagingVersion.WSReliableMessaging11;
                        serviceBuilder.AddServiceEndpoint<CallbackTriggerService, ITriggerServiceCoreWcf>(binding, Path);
                    });
                    next(builder);
                };
            }

            public Uri GetServiceUri(IWebHost host) =>
                new Uri($"{host.GetNetTcpAddressInUse()}{Path}");
        }

        // CoreWCF-side server contract.
        [CoreWCF.ServiceContract(Namespace = "http://corewcf/test", Name = "TriggerService", CallbackContract = typeof(ITriggerCallbackCoreWcf))]
        internal interface ITriggerServiceCoreWcf
        {
            [CoreWCF.OperationContract(IsOneWay = true, Action = "http://corewcf/test/TriggerService/TriggerBackgroundCallback")]
            void TriggerBackgroundCallback(string payload);
        }

        [CoreWCF.ServiceContract(Namespace = "http://corewcf/test", Name = "TriggerCallback")]
        internal interface ITriggerCallbackCoreWcf
        {
            [CoreWCF.OperationContract(IsOneWay = true, Action = "http://corewcf/test/TriggerService/Callback")]
            void Callback(string payload);
        }

        // Client-side mirror.
        [System.ServiceModel.ServiceContract(Namespace = "http://corewcf/test", Name = "TriggerService", CallbackContract = typeof(ITriggerCallback))]
        public interface ITriggerService
        {
            [System.ServiceModel.OperationContract(IsOneWay = true, Action = "http://corewcf/test/TriggerService/TriggerBackgroundCallback")]
            void TriggerBackgroundCallback(string payload);
        }

        [System.ServiceModel.ServiceContract(Namespace = "http://corewcf/test", Name = "TriggerCallback")]
        public interface ITriggerCallback
        {
            [System.ServiceModel.OperationContract(IsOneWay = true, Action = "http://corewcf/test/TriggerService/Callback")]
            void Callback(string payload);
        }

        public sealed class CallbackHandler : ITriggerCallback
        {
            public ManualResetEvent Received { get; } = new ManualResetEvent(false);
            public string LastPayload { get; private set; }

            public void Callback(string payload)
            {
                LastPayload = payload;
                Received.Set();
            }
        }

        [CoreWCF.ServiceBehavior(ConcurrencyMode = CoreWCF.ConcurrencyMode.Multiple, InstanceContextMode = CoreWCF.InstanceContextMode.Single)]
        internal sealed class CallbackTriggerService : ITriggerServiceCoreWcf
        {
            private static readonly SemaphoreSlim s_gate = new(0, 1);
            private static Exception s_lastError;

            public static void Reset()
            {
                while (s_gate.CurrentCount > 0)
                {
                    s_gate.Wait(0);
                }
                s_lastError = null;
            }

            public static void ReleaseGate() => s_gate.Release();
            public static Exception LastError => s_lastError;

            public void TriggerBackgroundCallback(string payload)
            {
                ITriggerCallbackCoreWcf cb = CoreWCF.OperationContext.Current.GetCallbackChannel<ITriggerCallbackCoreWcf>();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await s_gate.WaitAsync();
                        cb.Callback(payload);
                    }
                    catch (Exception ex)
                    {
                        s_lastError = ex;
                    }
                });
            }
        }
    }
}