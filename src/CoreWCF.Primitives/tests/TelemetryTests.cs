// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]
namespace CoreWCF.Primitives.Tests;

[Collection("TelemetryTests")]
public class TelemetryTests
{
    private const string TraceContextNamespace = "https://www.w3.org/TR/trace-context/";
    private const string TraceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";
    private const string TraceState = "vendor=value";

    [Fact]
    public async Task Basic_Telemetry_Test()
    {
        string telemetryEchoAction = "http://tempuri.org/ISimpleTelemetryService/Echo";
        var startedActivities = new ConcurrentBag<Activity>();
        var stoppedActivities = new ConcurrentBag<Activity>();

        // ShouldListenTo must be set to its final predicate before AddActivityListener is called.
        // ActivitySource attaches a listener at AddActivityListener time (for existing sources) and at
        // ActivitySource construction time (for sources created later). In both cases ShouldListenTo is
        // evaluated only once per (listener, source) pair; mutating ShouldListenTo afterwards does NOT
        // retroactively attach the listener to an already-existing source. The static
        // WcfInstrumentationActivitySource.ActivitySource may be initialized by a concurrent test
        // (e.g. DispatchBuilderTests in the default xUnit collection) before this test reaches
        // AddActivityListener, so a "_ => false" predicate at that moment would permanently prevent
        // attachment regardless of subsequent updates. Filtering by the unique DisplayName below
        // ensures activities created by concurrent tests do not interfere with the assertions.
        using var listener = new ActivityListener
        {
            ShouldListenTo = activitySource => activitySource.Name == "CoreWCF.Primitives",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => startedActivities.Add(activity),
            ActivityStopped = activity => stoppedActivities.Add(activity)
        };

        string serviceAddress = "http://localhost/dummy";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServiceModelServices();
        IServer server = new MockServer();
        services.AddSingleton(server);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.RegisterApplicationLifetime();
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IServiceBuilder serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
        serviceBuilder.BaseAddresses.Add(new Uri(serviceAddress));
        serviceBuilder.AddService<SimpleTelemetryService>();
        var binding = new CustomBinding("BindingName", "BindingNS");
        binding.Elements.Add(new MockTransportBindingElement());
        serviceBuilder.AddServiceEndpoint<SimpleTelemetryService, ISimpleTelemetryService>(binding, serviceAddress);
        await serviceBuilder.OpenAsync(TestContext.Current.CancellationToken);
        IDispatcherBuilder dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
        System.Collections.Generic.List<IServiceDispatcher> dispatchers =
            dispatcherBuilder.BuildDispatchers(typeof(SimpleTelemetryService));
        Assert.Single(dispatchers);
        IServiceDispatcher serviceDispatcher = dispatchers[0];
        Assert.Equal("foo", serviceDispatcher.Binding.Scheme);
        Assert.Equal(serviceAddress, serviceDispatcher.BaseAddress.ToString());
        IChannel mockChannel = new MockReplyChannel(serviceProvider);
        IServiceChannelDispatcher dispatcher =
            await serviceDispatcher.CreateServiceChannelDispatcherAsync(mockChannel);
        var requestContext = TestRequestContext.Create(serviceAddress, telemetryEchoAction);

        ActivitySource.AddActivityListener(listener);
        await dispatcher.DispatchAsync(requestContext);
        Assert.True(await requestContext.WaitForReplyAsync(TestContext.Current.CancellationToken), "Dispatcher didn't send reply");
        requestContext.ValidateReply(telemetryEchoAction + "Response");

        // ActivityStarted/ActivityStopped callbacks run synchronously inside Activity.Start()/Stop(),
        // and CoreWCF stops the activity before sending the reply, so by the time WaitForReplyAsync
        // returns the activity should already be in the bags. Poll briefly with the test cancellation
        // token in case there is any small async window on a heavily loaded machine. If the activity
        // is genuinely missing this fails fast with an explicit message rather than a TaskCanceledException.
        using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        pollCts.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            while (!stoppedActivities.Any(a => a.DisplayName == telemetryEchoAction))
            {
                await Task.Delay(50, pollCts.Token);
            }
        }
        catch (OperationCanceledException) when (pollCts.IsCancellationRequested && !TestContext.Current.CancellationToken.IsCancellationRequested)
        {
            Assert.Fail($"No Activity with DisplayName '{telemetryEchoAction}' was captured within 30s. " +
                $"Started count: {startedActivities.Count}, Stopped count: {stoppedActivities.Count}. " +
                "This usually indicates the ActivityListener was not attached to the CoreWCF.Primitives ActivitySource.");
        }

        var startedActivity = Assert.Single(startedActivities, a => a.DisplayName == telemetryEchoAction);
        var stoppedActivity = Assert.Single(stoppedActivities, a => a.DisplayName == telemetryEchoAction);

        Assert.Equal("CoreWCF.Primitives.IncomingRequest", startedActivity.OperationName);
        Assert.Equal("CoreWCF.Primitives.IncomingRequest", stoppedActivity.OperationName);
        Assert.Equal(telemetryEchoAction, startedActivity.DisplayName);
        Assert.Equal(telemetryEchoAction, stoppedActivity.DisplayName);
        Assert.Equal(ActivityKind.Server, startedActivity.Kind);
        Assert.Equal(ActivityKind.Server, stoppedActivity.Kind);
        Assert.Equal(startedActivity.RootId, stoppedActivity.RootId);
        Assert.Equal(startedActivity.SpanId, stoppedActivity.SpanId);
        Assert.Equal(startedActivity.TraceId, stoppedActivity.TraceId);

        var startedTags = startedActivity.Tags.ToList();
        var stoppedTags = stoppedActivity.Tags.ToList();
        Assert.Equal(7, startedTags.Count);
        Assert.Equal(7, stoppedTags.Count);

        Assert.Equal("rpc.system", startedTags[0].Key);
        Assert.Equal("dotnet_wcf", startedTags[0].Value);

        Assert.Equal("rpc.method", startedTags[1].Key);
        Assert.Equal(telemetryEchoAction, startedTags[1].Value);

        Assert.Equal("soap.message_version", startedTags[2].Key);
        Assert.Equal("Soap11 (http://schemas.xmlsoap.org/soap/envelope/) AddressingNone (http://schemas.microsoft.com/ws/2005/05/addressing/none)", startedTags[2].Value);

        Assert.Equal("server.address", startedTags[3].Key);
        Assert.Equal("localhost", startedTags[3].Value);

        Assert.Equal("wcf.channel.scheme", startedTags[4].Key);
        Assert.Equal("http", startedTags[4].Value);

        Assert.Equal("wcf.channel.path", startedTags[5].Key);
        Assert.Equal("/dummy", startedTags[5].Value);

        Assert.Equal("soap.reply_action", startedTags[6].Key);
        Assert.Equal(telemetryEchoAction + "Response", startedTags[6].Value);

        Assert.Equivalent(startedTags[1], stoppedTags[1]);
        Assert.Equivalent(startedTags[2], stoppedTags[2]);
        Assert.Equivalent(startedTags[3], stoppedTags[3]);
        Assert.Equivalent(startedTags[4], stoppedTags[4]);
        Assert.Equivalent(startedTags[5], stoppedTags[5]);
        Assert.Equivalent(startedTags[6], stoppedTags[6]);
    }

    [Fact]
    public async Task Soap_Trace_Context_Is_Used_As_Remote_Parent_Without_Ambient_Activity()
    {
        DispatchResult result = await DispatchAndCaptureActivityAsync(traceParent: TraceParent);
        Assert.True(ActivityContext.TryParse(TraceParent, null, isRemote: true, out ActivityContext expectedParent));

        Assert.Equal(expectedParent.TraceId, result.Activity.TraceId);
        Assert.Equal(expectedParent.SpanId, result.Activity.ParentSpanId);
        Assert.True(result.SampledParentContext.IsRemote);
    }

    [Fact]
    public async Task Missing_Trace_Context_Creates_Root_Activity()
    {
        DispatchResult result = await DispatchAndCaptureActivityAsync();

        Assert.Equal(default, result.Activity.ParentSpanId);
        Assert.Equal(default, result.SampledParentContext);
    }

    [Fact]
    public async Task Malformed_Trace_Parent_Is_Ignored()
    {
        DispatchResult result = await DispatchAndCaptureActivityAsync(traceParent: "not-a-trace-parent");

        Assert.Equal(default, result.Activity.ParentSpanId);
        Assert.Equal(default, result.SampledParentContext);
    }

    [Fact]
    public async Task Ambient_Activity_Takes_Precedence_Over_Soap_Trace_Context()
    {
        var ambientParentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded);

        DispatchResult result = await DispatchAndCaptureActivityAsync(
            traceParent: TraceParent,
            ambientParentContext: ambientParentContext);

        Assert.Equal(result.AmbientContext.TraceId, result.Activity.TraceId);
        Assert.Equal(result.AmbientContext.SpanId, result.Activity.ParentSpanId);
        Assert.False(result.SampledParentContext.IsRemote);
    }

    [Fact]
    public async Task Soap_Trace_State_Is_Propagated()
    {
        DispatchResult result = await DispatchAndCaptureActivityAsync(
            traceParent: TraceParent,
            traceState: TraceState);

        Assert.Equal(TraceState, result.Activity.TraceStateString);
        Assert.Equal(TraceState, result.SampledParentContext.TraceState);
    }

    [Fact]
    public async Task Trace_Context_In_Different_Namespace_Is_Ignored()
    {
        DispatchResult result = await DispatchAndCaptureActivityAsync(
            traceParent: TraceParent,
            headerNamespace: "urn:not-w3c-trace-context");

        Assert.Equal(default, result.Activity.ParentSpanId);
        Assert.Equal(default, result.SampledParentContext);
    }

    [Fact]
    public async Task Duplicate_Trace_Parent_Is_Ignored()
    {
        DispatchResult result = await DispatchAndCaptureActivityAsync(
            traceParent: TraceParent,
            duplicateTraceParent: true);

        Assert.Equal(default, result.Activity.ParentSpanId);
        Assert.Equal(default, result.SampledParentContext);
    }

    private static async Task<DispatchResult> DispatchAndCaptureActivityAsync(
        string traceParent = null,
        string traceState = null,
        string headerNamespace = TraceContextNamespace,
        bool duplicateTraceParent = false,
        ActivityContext? ambientParentContext = null)
    {
        const string telemetryEchoAction = "http://tempuri.org/ISimpleTelemetryService/Echo";
        const string serviceAddress = "http://localhost/dummy";
        var stoppedActivities = new ConcurrentBag<Activity>();

        // ActivityListener is global, so sampling callbacks can also run for concurrent CoreWCF requests.
        // Correlate each sampled parent with the activity created from that sampling decision.
        var sampledParentContexts = new ConcurrentDictionary<ActivityTraceId, ActivityContext>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = activitySource => activitySource.Name == "CoreWCF.Primitives",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
            {
                sampledParentContexts[options.TraceId] = options.Parent;
                return ActivitySamplingResult.AllData;
            },
            ActivityStopped = activity => stoppedActivities.Add(activity)
        };

        Activity previousActivity = Activity.Current;
        Activity ambientActivity = null;
        Activity.Current = null;

        try
        {
            if (ambientParentContext.HasValue)
            {
                ActivityContext parent = ambientParentContext.Value;
                ambientActivity = new Activity("ambient")
                    .SetIdFormat(ActivityIdFormat.W3C)
                    .SetParentId(parent.TraceId, parent.SpanId, parent.TraceFlags)
                    .Start();
            }

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddServiceModelServices();
            IServer server = new MockServer();
            services.AddSingleton(server);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.RegisterApplicationLifetime();
            using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IServiceBuilder serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
            serviceBuilder.BaseAddresses.Add(new Uri(serviceAddress));
            serviceBuilder.AddService<SimpleTelemetryService>();
            var binding = new CustomBinding("BindingName", "BindingNS");
            binding.Elements.Add(new MockTransportBindingElement());
            serviceBuilder.AddServiceEndpoint<SimpleTelemetryService, ISimpleTelemetryService>(binding, serviceAddress);
            await serviceBuilder.OpenAsync(TestContext.Current.CancellationToken);
            IDispatcherBuilder dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
            System.Collections.Generic.List<IServiceDispatcher> dispatchers =
                dispatcherBuilder.BuildDispatchers(typeof(SimpleTelemetryService));
            IServiceChannelDispatcher dispatcher =
                await Assert.Single(dispatchers).CreateServiceChannelDispatcherAsync(new MockReplyChannel(serviceProvider));
            var requestContext = TestRequestContext.Create(serviceAddress, telemetryEchoAction);

            if (traceParent != null)
            {
                requestContext.RequestMessage.Headers.Add(
                    MessageHeader.CreateHeader("traceparent", headerNamespace, traceParent));
            }

            if (duplicateTraceParent)
            {
                requestContext.RequestMessage.Headers.Add(
                    MessageHeader.CreateHeader("traceparent", headerNamespace, traceParent));
            }

            if (traceState != null)
            {
                requestContext.RequestMessage.Headers.Add(
                    MessageHeader.CreateHeader("tracestate", headerNamespace, traceState));
            }

            ActivitySource.AddActivityListener(listener);
            await dispatcher.DispatchAsync(requestContext);
            Assert.True(
                await requestContext.WaitForReplyAsync(TestContext.Current.CancellationToken),
                "Dispatcher didn't send reply");

            Activity activity = Assert.Single(stoppedActivities, a => a.DisplayName == telemetryEchoAction);
            Assert.True(sampledParentContexts.TryGetValue(activity.TraceId, out ActivityContext sampledParentContext));
            return new DispatchResult(activity, sampledParentContext, ambientActivity?.Context ?? default);
        }
        finally
        {
            ambientActivity?.Stop();
            Activity.Current = previousActivity;
        }
    }

    private sealed class DispatchResult
    {
        public DispatchResult(Activity activity, ActivityContext sampledParentContext, ActivityContext ambientContext)
        {
            Activity = activity;
            SampledParentContext = sampledParentContext;
            AmbientContext = ambientContext;
        }

        public Activity Activity { get; }

        public ActivityContext SampledParentContext { get; }

        public ActivityContext AmbientContext { get; }
    }

    [CoreWCF.ServiceContract]
    [System.ServiceModel.ServiceContract]
    public interface ISimpleTelemetryService
    {
        [CoreWCF.OperationContract]
        [System.ServiceModel.OperationContract]
        string Echo(string echo);
    }

    internal class SimpleTelemetryService : ISimpleTelemetryService
    {
        public string Echo(string echo)
        {
            return echo;
        }
    }
}
