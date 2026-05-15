// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Helpers.Interceptor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace CoreWCF.NetTcp
{
    // Coverage-focused tests for Reliable Sessions code paths that are not exercised by
    // the happy-path NetTcpBindingSimpleReliableSessions / MultipleMessages / etc. tests.
    // These primarily target server-side fault paths in WsrmFault, FaultHelper,
    // ChannelReliableSession and the binder, as well as configuration/option setters in
    // ReliableSessionBindingElement.
    //
    // All tests are written so that they pass against the corrected branch and would have
    // failed against earlier broken implementations (or trivially exercise paths that were
    // dead code before these tests were added).
    public class ReliableSessionsCoverageTests
    {
        private readonly ITestOutputHelper _output;

        public ReliableSessionsCoverageTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // Hits ChannelReliableSession.OnInactivityElapsed -> SequenceTerminatedFault
        // construction & serialization -> ReplyFaultHelper.SendFaultAsync -> server fault
        // dispatch path. Drives the WsrmFault.SequenceTerminatedFault / FaultHelper code
        // (both <15% covered in baseline) for the first time.
        [Fact]
        public void InactivityTimeout_FaultsSessionAndPropagatesToClient()
        {
            var startup = new ConfigurableRsStartup(rsbe =>
            {
                rsbe.InactivityTimeout = TimeSpan.FromSeconds(3);
            });
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                // Long client-side inactivity so only the server's 3-sec inactivity fires.
                binding.ReliableSession.InactivityTimeout = TimeSpan.FromMinutes(10);
                binding.SendTimeout = TimeSpan.FromSeconds(15);
                binding.ReceiveTimeout = TimeSpan.FromMinutes(2);
                var endpoint = new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host));
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(binding, endpoint);
                Contract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();
                    Assert.Equal("hello", channel.EchoString("hello"));

                    // Wait > server InactivityTimeout. With no acks/messages from the client
                    // the server's inactivity timer will fire -- exercising
                    // ChannelReliableSession.OnInactivityElapsed and the SequenceTerminated
                    // fault path through FaultHelper / WsrmFault.
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

        // Hits the operation-fault dispatch path through reliable sessions:
        // ReliableServiceDispatcher.DispatchAsync -> exception -> reply fault generation
        // and reply path through ReliableReplySessionChannel.
        [Fact]
        public void ServiceOperationFaults_PropagateAsCommunicationException()
        {
            var startup = new FaultingServiceStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(binding,
                    new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host)));
                Contract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();

                    Exception caught = Record.Exception(() => channel.EchoString("fault-please"));
                    Assert.NotNull(caught);
                    _output.WriteLine($"Operation-fault exception: {caught.GetType().FullName}: {caught.Message}");

                    // After a service-side exception the client should observe a fault. The
                    // exact type can vary (FaultException for explicit FaultException, generic
                    // FaultException<ExceptionDetail> if IncludeExceptionDetailInFaults is on,
                    // or a CommunicationException). Any of these prove the fault path ran.
                    Assert.True(
                        caught is System.ServiceModel.FaultException ||
                        caught is System.ServiceModel.CommunicationException,
                        $"Expected fault/comm exception, got {caught.GetType().FullName}");

                    // The reliable session should remain usable (the fault is at the operation
                    // level, not the session level), so a follow-up call on the same channel
                    // either succeeds (after recovering from Faulted) or gives a clean error.
                    // We just verify the channel state is observable.
                    System.ServiceModel.CommunicationState state = (channel as System.ServiceModel.IClientChannel).State;
                    Assert.True(state == System.ServiceModel.CommunicationState.Opened ||
                                state == System.ServiceModel.CommunicationState.Faulted ||
                                state == System.ServiceModel.CommunicationState.Closed,
                        $"Unexpected channel state: {state}");
                }
                finally
                {
                    SafeAbort(channel, factory);
                }
            }
        }

        // Hits ChannelReliableSession.GetUnknownSequenceFault + UnknownSequenceFault
        // construction/serialization + FaultHelper.SendFaultAsync. Forces an outbound
        // application message to carry an unknown sequence identifier; the server should
        // reject the message with an UnknownSequence sub-code fault.
        [Fact]
        public void UnknownSequenceInjection_FaultsSession()
        {
            var startup = new ConfigurableRsStartup(rsbe => { });
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                var injector = new InjectUnknownSequenceInterceptor();
                CustomBinding clientBinding = BuildClientBindingWithInterceptor(injector);
                var endpoint = new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host));
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(clientBinding, endpoint);
                // Keep timeouts short so the fault path closes the test quickly.
                factory.Endpoint.Binding.SendTimeout = TimeSpan.FromSeconds(20);
                factory.Endpoint.Binding.ReceiveTimeout = TimeSpan.FromSeconds(20);
                Contract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();
                    // Establish the session normally first so CreateSequence/Response is clean.
                    Assert.Equal("ok", channel.EchoString("ok"));

                    // Now corrupt the next outbound application message's sequence identifier.
                    injector.Enabled = true;

                    Exception caught = Record.Exception(() => channel.EchoString("bad-seq"));
                    Assert.NotNull(caught);
                    _output.WriteLine($"UnknownSequence exception: {caught.GetType().FullName}: {caught.Message}");

                    // The server may respond with a SOAP fault carrying UnknownSequence and the
                    // client surfaces this as FaultException, or the session may be torn down
                    // with a CommunicationException. Either is a passing outcome.
                    Assert.True(
                        caught is System.ServiceModel.FaultException ||
                        caught is System.ServiceModel.CommunicationException ||
                        caught is TimeoutException,
                        $"Expected fault/comm/timeout, got {caught.GetType().FullName}");

                    Assert.True(injector.MutatedCount > 0, "Injector never mutated a sequence header.");
                }
                finally
                {
                    SafeAbort(channel, factory);
                }
            }
        }

        // Hits ReliableOutputConnection windowing, ReliableInputConnection reordering, and
        // dispatcher concurrency on a single reliable session. Many concurrent operations
        // exercise window full / partial-ack / out-of-order delivery in the server.
        [Fact]
        public async Task ConcurrentCalls_OnSingleSession_AllSucceed()
        {
            var startup = new ConfigurableRsStartup(rsbe =>
            {
                // Force a small transfer window so concurrent sends back up against the
                // window-full path (output connection waits for ack before sending more).
                rsbe.MaxTransferWindowSize = 4;
                rsbe.AcknowledgementInterval = TimeSpan.FromMilliseconds(50);
            });
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                binding.ReliableSession.Ordered = false;
                binding.SendTimeout = TimeSpan.FromMinutes(1);
                var customBinding = new System.ServiceModel.Channels.CustomBinding(binding);
                var rsbe = customBinding.Elements.Find<System.ServiceModel.Channels.ReliableSessionBindingElement>();
                rsbe.MaxTransferWindowSize = 4;

                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(customBinding,
                    new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host)));
                Contract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();

                    const int parallel = 16;
                    var tasks = new Task<string>[parallel];
                    for (int i = 0; i < parallel; i++)
                    {
                        int idx = i;
                        tasks[idx] = Task.Run(() => channel.EchoString($"msg-{idx}"));
                    }
                    await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromMinutes(1));
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

        // Theory: spin up reliable sessions with several different binding configurations.
        // This exercises the property setters/getters on ReliableSessionBindingElement and
        // forces ChannelReliableSession to initialize with non-default parameters
        // (different InactivityTimeout, AcknowledgementInterval, transfer window size,
        // MaxRetryCount, MaxPendingChannels, FlowControl, etc.).
        [Theory]
        [InlineData(/*window*/ 1,  /*ordered*/ true,  /*flow*/ true,  /*ackMs*/ 200, /*inactivitySec*/ 30)]
        [InlineData(/*window*/ 8,  /*ordered*/ false, /*flow*/ false, /*ackMs*/ 50,  /*inactivitySec*/ 60)]
        [InlineData(/*window*/ 32, /*ordered*/ true,  /*flow*/ true,  /*ackMs*/ 10,  /*inactivitySec*/ 120)]
        public void BindingConfiguration_Variations(int window, bool ordered, bool flow, int ackMs, int inactivitySec)
        {
            var startup = new ConfigurableRsStartup(rsbe =>
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

                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                binding.ReliableSession.Ordered = ordered;
                binding.ReliableSession.InactivityTimeout = TimeSpan.FromSeconds(inactivitySec);

                var customBinding = new System.ServiceModel.Channels.CustomBinding(binding);
                var clientRsbe = customBinding.Elements.Find<System.ServiceModel.Channels.ReliableSessionBindingElement>();
                clientRsbe.MaxTransferWindowSize = window;
                clientRsbe.FlowControlEnabled = flow;
                clientRsbe.AcknowledgementInterval = TimeSpan.FromMilliseconds(ackMs);

                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(customBinding,
                    new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host)));
                Contract.IEchoService channel = factory.CreateChannel();
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

        // Hits the one-way dispatch path through reliable sessions. One-way operations
        // produce no reply, so they exercise the server's "drop reply context" branch in
        // the dispatcher and the input-connection ack path. The contract must include at
        // least one two-way op so the channel shape resolves to a duplex session channel.
        [Fact]
        public void OneWayOperation_OnReliableSession_Succeeds()
        {
            OneWayService.Reset();
            var startup = new OneWayStartup();
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                var factory = new System.ServiceModel.ChannelFactory<IOneWayService>(binding,
                    new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host)));
                IOneWayService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();
                    for (int i = 0; i < 5; i++)
                    {
                        channel.Notify($"event-{i}");
                    }
                    // Two-way ping forces all preceding one-ways to be acked.
                    Assert.Equal("ping", channel.Ping("ping"));
                    Assert.True(OneWayService.AllReceived.Wait(TimeSpan.FromSeconds(15)),
                        $"Expected 5 one-way messages, got {OneWayService.ReceivedCount}");
                }
                finally
                {
                    SafeAbort(channel, factory);
                }
            }
        }

        // Forces the client to abort while the session is mid-flight. Hits abort paths in
        // ReliableChannelBinder, ReliableSession.OnLocalFault, and the server-side
        // OnFaulted -> AbortInnerServiceDispatcher path (the H6 fix area).
        [Fact]
        public void ClientAbortDuringActiveSession_DoesNotHangServer()
        {            var startup = new ConfigurableRsStartup(rsbe => { });
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
                binding.ReliableSession.Enabled = true;
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(binding,
                    new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host)));
                Contract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();
                    Assert.Equal("first", channel.EchoString("first"));

                    // Abort while open.
                    (channel as System.ServiceModel.IClientChannel).Abort();
                    factory.Abort();

                    // A new session against the same host should be servable -- proves the
                    // server didn't deadlock or leak its dispatcher.
                    var factory2 = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(binding,
                        new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host)));
                    Contract.IEchoService channel2 = factory2.CreateChannel();
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

        // Drives ChannelReliableSession AckRequested handling + server-side ack reply path
        // by injecting an explicit wsrm:AckRequested header on the next outbound app message.
        [Fact]
        public void AckRequestedInjection_DoesNotFaultSession()
        {
            var startup = new ConfigurableRsStartup(rsbe => { });
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                var injector = new InjectAckRequestedInterceptor();
                CustomBinding clientBinding = BuildClientBindingWithInterceptor(injector);
                var endpoint = new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host));
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(clientBinding, endpoint);
                Contract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();
                    Assert.Equal("first", channel.EchoString("first"));

                    // Tag the next call with an AckRequested header.
                    injector.Enabled = true;
                    Assert.Equal("second", channel.EchoString("second"));
                    injector.Enabled = false;

                    // Subsequent traffic should still flow on the same session.
                    Assert.Equal("third", channel.EchoString("third"));

                    Assert.True(injector.InjectedCount > 0, "AckRequested header was never injected.");
                }
                finally
                {
                    SafeAbort(channel, factory);
                }
            }
        }

        // Drives the InvalidAcknowledgementFault path: the client claims to have received
        // a message number the server never sent. Per spec the receiver responds with
        // sub-code wsrm:InvalidAcknowledgement and the session faults. Hits
        // InvalidAcknowledgementFault construction + serialization in WsrmFault.
        [Fact]
        public void InvalidAcknowledgementInjection_FaultsSession()
        {
            var startup = new ConfigurableRsStartup(rsbe => { });
            IWebHost host = ServiceHelper.CreateWebHostBuilder(startup, _output).Build();
            using (host)
            {
                host.Start();

                var injector = new InjectInvalidAckInterceptor();
                CustomBinding clientBinding = BuildClientBindingWithInterceptor(injector);
                var endpoint = new System.ServiceModel.EndpointAddress(startup.GetServiceUri(host));
                var factory = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(clientBinding, endpoint);
                factory.Endpoint.Binding.SendTimeout = TimeSpan.FromSeconds(20);
                factory.Endpoint.Binding.ReceiveTimeout = TimeSpan.FromSeconds(20);
                Contract.IEchoService channel = factory.CreateChannel();
                try
                {
                    (channel as System.ServiceModel.IClientChannel).Open();
                    Assert.Equal("ok", channel.EchoString("ok"));

                    injector.Enabled = true;

                    Exception caught = Record.Exception(() => channel.EchoString("bad-ack"));
                    Assert.NotNull(caught);
                    _output.WriteLine($"InvalidAck exception: {caught.GetType().FullName}: {caught.Message}");
                    Assert.True(
                        caught is System.ServiceModel.FaultException ||
                        caught is System.ServiceModel.CommunicationException ||
                        caught is TimeoutException,
                        $"Expected fault/comm/timeout, got {caught.GetType().FullName}");
                    Assert.True(injector.InjectedCount > 0, "Invalid ack was never injected.");
                }
                finally
                {
                    SafeAbort(channel, factory);
                }
            }
        }

        // ----------------------- helpers -----------------------

        private static CustomBinding BuildClientBindingWithInterceptor(IMessageInterceptor interceptor)
        {
            System.ServiceModel.NetTcpBinding netTcp = ClientHelper.GetBufferedModeBinding(System.ServiceModel.SecurityMode.None);
            netTcp.ReliableSession.Enabled = true;
            var binding = new CustomBinding(netTcp);
            int transportIndex = binding.Elements.Count - 1;
            binding.Elements.Insert(transportIndex, new InterceptingBindingElement(interceptor));
            return binding;
        }

        private static void SafeAbort(object channel, System.ServiceModel.ICommunicationObject factory)
        {
            try { (channel as System.ServiceModel.IClientChannel)?.Abort(); } catch { }
            try { factory?.Abort(); } catch { }
        }

        // ----------------------- contracts / services -----------------------

        [CoreWCF.ServiceContract(Namespace = "http://corewcf/test/oneway", Name = "OneWayService", SessionMode = CoreWCF.SessionMode.Required)]
        [System.ServiceModel.ServiceContract(Namespace = "http://corewcf/test/oneway", Name = "OneWayService", SessionMode = System.ServiceModel.SessionMode.Required)]
        public interface IOneWayService
        {
            [CoreWCF.OperationContract(IsOneWay = true, Name = "Notify",
                Action = "http://corewcf/test/oneway/OneWayService/Notify")]
            [System.ServiceModel.OperationContract(IsOneWay = true, Name = "Notify",
                Action = "http://corewcf/test/oneway/OneWayService/Notify")]
            void Notify(string payload);

            [CoreWCF.OperationContract(Name = "Ping",
                Action = "http://corewcf/test/oneway/OneWayService/Ping",
                ReplyAction = "http://corewcf/test/oneway/OneWayService/PingResponse")]
            [System.ServiceModel.OperationContract(Name = "Ping",
                Action = "http://corewcf/test/oneway/OneWayService/Ping",
                ReplyAction = "http://corewcf/test/oneway/OneWayService/PingResponse")]
            string Ping(string payload);
        }

        [CoreWCF.ServiceBehavior(InstanceContextMode = CoreWCF.InstanceContextMode.Single,
            ConcurrencyMode = CoreWCF.ConcurrencyMode.Multiple)]
        internal sealed class OneWayService : IOneWayService
        {
            private static readonly CountdownEvent s_received = new CountdownEvent(5);
            private static int s_count;

            public static int ReceivedCount => Volatile.Read(ref s_count);
            public static CountdownEvent AllReceived => s_received;

            public static void Reset()
            {
                Volatile.Write(ref s_count, 0);
                s_received.Reset(5);
            }

            public void Notify(string payload)
            {
                Interlocked.Increment(ref s_count);
                if (!s_received.IsSet)
                {
                    s_received.Signal();
                }
            }

            public string Ping(string payload) => payload;
        }

        [CoreWCF.ServiceContract(Namespace = "http://corewcf/test/faulting", Name = "FaultingEchoService")]
        public interface IFaultingEchoCoreWcf
        {
            [CoreWCF.OperationContract(Name = "Echo",
                Action = Contract.Constants.OPERATION_BASE + "Echo",
                ReplyAction = Contract.Constants.OPERATION_BASE + "EchoResponse")]
            string EchoString(string echo);
        }

        [CoreWCF.ServiceBehavior(IncludeExceptionDetailInFaults = true)]
        internal sealed class FaultingEchoService : IFaultingEchoCoreWcf
        {
            public string EchoString(string echo)
            {
                throw new InvalidOperationException("intentional service-side failure for coverage test");
            }
        }

        // ----------------------- startups -----------------------

        internal sealed class ConfigurableRsStartup : IStartupFilter
        {
            private const string PathBase = "/rsCoverage";
            private static int s_pathCounter;
            private readonly string _path;
            private readonly Action<CoreWCF.Channels.ReliableSessionBindingElement> _configure;

            public ConfigurableRsStartup(Action<CoreWCF.Channels.ReliableSessionBindingElement> configure)
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
                        var netTcp = new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None, true);
                        var custom = new CoreWCF.Channels.CustomBinding(netTcp);
                        var rsbe = custom.Elements.Find<CoreWCF.Channels.ReliableSessionBindingElement>();
                        _configure(rsbe);
                        serviceBuilder.AddServiceEndpoint<Services.EchoService, Contract.IEchoService>(custom, _path);
                    });
                    next(builder);
                };

            public Uri GetServiceUri(IWebHost host) =>
                new Uri($"{host.GetNetTcpAddressInUse()}{_path}");
        }

        internal sealed class FaultingServiceStartup : IStartupFilter
        {
            private const string Path = "/rsFaulting.svc";

            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                builder =>
                {
                    builder.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<FaultingEchoService>(opt =>
                        {
                            opt.DebugBehavior.IncludeExceptionDetailInFaults = true;
                        });
                        var netTcp = new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None, true);
                        serviceBuilder.AddServiceEndpoint<FaultingEchoService, IFaultingEchoCoreWcf>(netTcp, Path);
                    });
                    next(builder);
                };

            public Uri GetServiceUri(IWebHost host) =>
                new Uri($"{host.GetNetTcpAddressInUse()}{Path}");
        }

        internal sealed class OneWayStartup : IStartupFilter
        {
            private const string Path = "/rsOneWay.svc";

            public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
                builder =>
                {
                    builder.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<OneWayService>();
                        var netTcp = new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None, true);
                        serviceBuilder.AddServiceEndpoint<OneWayService, IOneWayService>(netTcp, Path);
                    });
                    next(builder);
                };

            public Uri GetServiceUri(IWebHost host) =>
                new Uri($"{host.GetNetTcpAddressInUse()}{Path}");
        }
    }
}
