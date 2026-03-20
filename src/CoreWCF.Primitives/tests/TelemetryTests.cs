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
    [Fact]
    public async Task Basic_Telemetry_Test()
    {
        string telemetryEchoAction = "http://tempuri.org/ISimpleTelemetryService/Echo";
        var startedActivities = new ConcurrentBag<Activity>();
        var stoppedActivities = new ConcurrentBag<Activity>();

        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => false,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => startedActivities.Add(activity),
            ActivityStopped = activity => stoppedActivities.Add(activity)
        };

        string actionSuffix = "TelemetryTest";
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
        listener.ShouldListenTo = activitySource => activitySource.Name == "CoreWCF.Primitives";
        await dispatcher.DispatchAsync(requestContext);
        listener.ShouldListenTo = _ => false;
        Assert.True(await requestContext.WaitForReplyAsync(TestContext.Current.CancellationToken), "Dispatcher didn't send reply");
        requestContext.ValidateReply(telemetryEchoAction + "Response");

        // Other tests running in parallel may have started activities, so we filter for the specific activity we expect
        // and verify there's only one of the activity we expect.
        CancellationTokenSource timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using (var registration = TestContext.Current.CancellationToken.Register(() => timeoutCts.Cancel()))
        {
            while (!startedActivities.Any(a => a.DisplayName == telemetryEchoAction) && !stoppedActivities.Any(a => a.DisplayName == telemetryEchoAction))
            {
                await Task.Delay(100, timeoutCts.Token);
            }
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
