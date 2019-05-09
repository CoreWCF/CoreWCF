//using Microsoft.Extensions.DependencyInjection;
using Helpers;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceModel.Channels;
using Microsoft.ServiceModel.Configuration;
using Microsoft.ServiceModel.Primitives.Tests;
using System.Threading;
using Xunit;

public static class DispatchBuilderTests
{
    [Fact]
    public static void BuildDispatcherWithConfiguration()
    {
        string serviceAddress = "http://localhost/dummy";
        var services = new ServiceCollection();
        services.AddServiceModelServices();
        var serverAddressesFeature = new ServerAddressesFeature();
        serverAddressesFeature.Addresses.Add(serviceAddress);
        IServer server = new MockServer();
        server.Features.Set<IServerAddressesFeature>(serverAddressesFeature);
        services.AddSingleton(server);
        var serviceProvider = services.BuildServiceProvider();
        var serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
        serviceBuilder.AddService<SimpleService>();
        var binding = new CustomBinding("BindingName", "BindingNS");
        binding.Elements.Add(new MockTransportBindingElement());
        serviceBuilder.AddServiceEndpoint<SimpleService, ISimpleService>(binding, serviceAddress);
        var dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
        var dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleService));
        Assert.Single(dispatchers);
        var dispatcher = dispatchers[0];
        Assert.Equal("foo", dispatcher.Binding.Scheme);
        Assert.Equal(serviceAddress, dispatcher.BaseAddress.ToString());
        var requestContext = TestRequestContext.Create(serviceAddress);
        IChannel mockChannel = new MockReplyChannel(serviceProvider);
        dispatcher.DispatchAsync(requestContext, mockChannel, CancellationToken.None).Wait();
        requestContext.ValidateReply();
    }

    [Fact]
    public static void BuildDispatcherWithConfiguration_Singleton_Not_WellKnown()
    {
        string serviceAddress = "http://localhost/dummy";
        var services = new ServiceCollection();
        services.AddServiceModelServices();
        var serverAddressesFeature = new ServerAddressesFeature();
        serverAddressesFeature.Addresses.Add(serviceAddress);
        IServer server = new MockServer();
        server.Features.Set<IServerAddressesFeature>(serverAddressesFeature);
        services.AddSingleton(server);
        var serviceProvider = services.BuildServiceProvider();
        var serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
        serviceBuilder.AddService<SimpleSingletonService>();
        var binding = new CustomBinding("BindingName", "BindingNS");
        binding.Elements.Add(new MockTransportBindingElement());
        serviceBuilder.AddServiceEndpoint<SimpleSingletonService, ISimpleService>(binding, serviceAddress);
        var dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
        var dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleSingletonService));
        Assert.Single(dispatchers);
        var dispatcher = dispatchers[0];
        Assert.Equal("foo", dispatcher.Binding.Scheme);
        Assert.Equal(serviceAddress, dispatcher.BaseAddress.ToString());
        var requestContext = TestRequestContext.Create(serviceAddress);
        IChannel mockChannel = new MockReplyChannel(serviceProvider);
        dispatcher.DispatchAsync(requestContext, mockChannel, CancellationToken.None).Wait();
        requestContext.ValidateReply();
    }

    [Fact]
    public static void BuildDispatcherWithConfiguration_Singleton_WellKnown()
    {
        string serviceAddress = "http://localhost/dummy";
        var services = new ServiceCollection();
        services.AddServiceModelServices();
        var serverAddressesFeature = new ServerAddressesFeature();
        serverAddressesFeature.Addresses.Add(serviceAddress);
        IServer server = new MockServer();
        server.Features.Set<IServerAddressesFeature>(serverAddressesFeature);
        services.AddSingleton(server);
        services.AddSingleton(new SimpleSingletonService());
        var serviceProvider = services.BuildServiceProvider();
        var serviceBuilder = serviceProvider.GetRequiredService<IServiceBuilder>();
        serviceBuilder.AddService<SimpleSingletonService>();
        var binding = new CustomBinding("BindingName", "BindingNS");
        binding.Elements.Add(new MockTransportBindingElement());
        serviceBuilder.AddServiceEndpoint<SimpleSingletonService, ISimpleService>(binding, serviceAddress);
        var dispatcherBuilder = serviceProvider.GetRequiredService<IDispatcherBuilder>();
        var dispatchers = dispatcherBuilder.BuildDispatchers(typeof(SimpleSingletonService));
        Assert.Single(dispatchers);
        var dispatcher = dispatchers[0];
        Assert.Equal("foo", dispatcher.Binding.Scheme);
        Assert.Equal(serviceAddress, dispatcher.BaseAddress.ToString());
        var requestContext = TestRequestContext.Create(serviceAddress);
        IChannel mockChannel = new MockReplyChannel(serviceProvider);
        dispatcher.DispatchAsync(requestContext, mockChannel, CancellationToken.None).Wait();
        requestContext.ValidateReply();
    }
}
