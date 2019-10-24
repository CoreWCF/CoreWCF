using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Primitives.Tests.Helpers;
using Helpers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using Xunit;

namespace DependencyInjection
{
    public class ServiceInstanceContextModeTests
    {
        [Fact]
        public static void InstanceContextMode_Single()
        {
            SingleInstanceContextSimpleService.ClearCounts();
            var services = new ServiceCollection();
            var serviceInstance = new SingleInstanceContextSimpleService();
            services.AddSingleton(serviceInstance);
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<SingleInstanceContextSimpleService>(services, serviceAddress);
            IChannel mockChannel = new MockReplyChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            Assert.Equal(1, SingleInstanceContextSimpleService.AddBindingParametersCallCount);
            Assert.Equal(1, SingleInstanceContextSimpleService.ApplyDispatchBehaviorCount);
            Assert.Equal(1, SingleInstanceContextSimpleService.ValidateCallCount);

            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            Assert.Equal(1, SingleInstanceContextSimpleService.CreationCount);
            Assert.Equal(0, SingleInstanceContextSimpleService.DisposalCount);
            Assert.Equal(2, serviceInstance.CallCount);
        }

        [Fact]
        public static void InstanceContextMode_Single_NoInjection()
        {
            SingleInstanceContextSimpleService.ClearCounts();
            var services = new ServiceCollection();
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<SingleInstanceContextSimpleService>(services, serviceAddress);
            IChannel mockChannel = new MockReplyChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            Assert.Equal(1, SingleInstanceContextSimpleService.AddBindingParametersCallCount);
            Assert.Equal(1, SingleInstanceContextSimpleService.ApplyDispatchBehaviorCount);
            Assert.Equal(1, SingleInstanceContextSimpleService.ValidateCallCount);

            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            Assert.Equal(1, SingleInstanceContextSimpleService.CreationCount);
            Assert.Equal(0, SingleInstanceContextSimpleService.DisposalCount);
        }

        [Fact]
        public static void InstanceContextMode_PerCall()
        {
            PerCallInstanceContextSimpleServiceAndBehavior.ClearCounts();
            var services = new ServiceCollection();
            services.AddTransient<PerCallInstanceContextSimpleServiceAndBehavior>();
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<PerCallInstanceContextSimpleServiceAndBehavior>(services, serviceAddress);
            IChannel mockChannel = new MockReplyChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            // Instance created as part of service startup to probe if type is availale in DI
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.CreationCount);
            // Instance not disposed as it implements IServiceBehavior and is added to service behaviors
            Assert.Equal(0, PerCallInstanceContextSimpleServiceAndBehavior.DisposalCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.AddBindingParametersCallCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.ApplyDispatchBehaviorCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.ValidateCallCount);

            PerCallInstanceContextSimpleServiceAndBehavior.ClearCounts();
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            Assert.Equal(2, PerCallInstanceContextSimpleServiceAndBehavior.CreationCount);
            Assert.Equal(2, PerCallInstanceContextSimpleServiceAndBehavior.DisposalCount);
        }

        [Fact]
        public static void InstanceContextMode_PerCall_NoInjection()
        {
            PerCallInstanceContextSimpleService.ClearCounts();
            var services = new ServiceCollection();
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<PerCallInstanceContextSimpleService>(services, serviceAddress);
            IChannel mockChannel = new MockReplyChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            // Instance shouldn't be created as part of service startup as type isn't available in DI
            Assert.Equal(0, PerCallInstanceContextSimpleService.CreationCount);
            Assert.Equal(0, PerCallInstanceContextSimpleService.DisposalCount);

            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            Assert.Equal(2, PerCallInstanceContextSimpleService.CreationCount);
            Assert.Equal(2, PerCallInstanceContextSimpleService.DisposalCount);
        }

        [Fact]
        public static void InstanceContextMode_PerCall_WithBehavior_NoInjection()
        {
            PerCallInstanceContextSimpleServiceAndBehavior.ClearCounts();
            var services = new ServiceCollection();
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<PerCallInstanceContextSimpleServiceAndBehavior>(services, serviceAddress);
            IChannel mockChannel = new MockReplyChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            // Instance created as part of service startup as it implements IServiceBehavior
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.CreationCount);
            // Instance not disposed as it implements IServiceBehavior and is added to service behaviors
            Assert.Equal(0, PerCallInstanceContextSimpleServiceAndBehavior.DisposalCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.AddBindingParametersCallCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.ApplyDispatchBehaviorCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.ValidateCallCount);

            PerCallInstanceContextSimpleServiceAndBehavior.ClearCounts();
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            Assert.Equal(2, PerCallInstanceContextSimpleServiceAndBehavior.CreationCount);
            Assert.Equal(2, PerCallInstanceContextSimpleServiceAndBehavior.DisposalCount);
        }

        [Fact]
        public static void InstanceContextMode_PerSession()
        {
            PerSessionInstanceContextSimpleServiceAndBehavior.ClearCounts();
            var services = new ServiceCollection();
            services.AddTransient<PerSessionInstanceContextSimpleServiceAndBehavior>();
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<PerSessionInstanceContextSimpleServiceAndBehavior>(services, serviceAddress);
            IChannel mockChannel = new MockReplySessionChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            // Instance created as part of service startup to probe if type is available in DI
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.CreationCount);
            // Instance not disposed as it implements IServiceBehavior and is added to service behaviors
            Assert.Equal(0, PerSessionInstanceContextSimpleServiceAndBehavior.DisposalCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.AddBindingParametersCallCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.ApplyDispatchBehaviorCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.ValidateCallCount);

            PerSessionInstanceContextSimpleServiceAndBehavior.ClearCounts();
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            // Close channel/session by sending null request context
            dispatcher.DispatchAsync(null, CancellationToken.None).Wait();
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.CreationCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.DisposalCount);
        }

        [Fact]
        public static void InstanceContextMode_PerSession_NoInjection()
        {
            PerSessionInstanceContextSimpleService.ClearCounts();
            var services = new ServiceCollection();
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<PerSessionInstanceContextSimpleService>(services, serviceAddress);
            IChannel mockChannel = new MockReplySessionChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            // Instance shouldn't be created as part of service startup to as type isn't available in DI
            Assert.Equal(0, PerSessionInstanceContextSimpleService.CreationCount);
            Assert.Equal(0, PerSessionInstanceContextSimpleService.DisposalCount);

            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            // Close channel/session by sending null request context
            dispatcher.DispatchAsync(null, CancellationToken.None).Wait();
            Assert.Equal(1, PerSessionInstanceContextSimpleService.CreationCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleService.DisposalCount);
        }

        [Fact]
        public static void InstanceContextMode_PerSession_WithBehavior_NoInjection()
        {
            PerSessionInstanceContextSimpleServiceAndBehavior.ClearCounts();
            var services = new ServiceCollection();
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<PerSessionInstanceContextSimpleServiceAndBehavior>(services, serviceAddress);
            IChannel mockChannel = new MockReplySessionChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            // Instance created as part of service startup as it implements IServiceBehavior
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.CreationCount);
            // Instance not disposed as it implements IServiceBehavior and is added to service behaviors
            Assert.Equal(0, PerSessionInstanceContextSimpleServiceAndBehavior.DisposalCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.AddBindingParametersCallCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.ApplyDispatchBehaviorCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.ValidateCallCount);

            PerSessionInstanceContextSimpleServiceAndBehavior.ClearCounts();
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            // Close channel/session by sending null request context
            dispatcher.DispatchAsync(null, CancellationToken.None).Wait();
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.CreationCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.DisposalCount);
        }
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class SingleInstanceContextSimpleService : InstanceContextSimpleServiceAndBehaviorBase<SingleInstanceContextSimpleService> { }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class PerCallInstanceContextSimpleService : InstanceContextSimpleServiceBase<PerCallInstanceContextSimpleService> { }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class PerCallInstanceContextSimpleServiceAndBehavior : InstanceContextSimpleServiceAndBehaviorBase<PerCallInstanceContextSimpleServiceAndBehavior> { }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    public class PerSessionInstanceContextSimpleService : InstanceContextSimpleServiceBase<PerSessionInstanceContextSimpleService> { }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    public class PerSessionInstanceContextSimpleServiceAndBehavior : InstanceContextSimpleServiceAndBehaviorBase<PerSessionInstanceContextSimpleServiceAndBehavior> { }

    public abstract class InstanceContextSimpleServiceAndBehaviorBase<TService> : InstanceContextSimpleServiceBase<TService>, IServiceBehavior where TService : InstanceContextSimpleServiceAndBehaviorBase<TService>
    {

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            AddBindingParametersCallCount++;
        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            ApplyDispatchBehaviorCount++;
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            if (IsDisposed) throw new ObjectDisposedException(GetType().Name);
            ValidateCallCount++;
        }
    }

    public abstract class InstanceContextSimpleServiceBase<TService> : ISimpleService, IDisposable where TService : InstanceContextSimpleServiceBase<TService>
    {
        public static int CreationCount { get; set; }
        public static int DisposalCount { get; set; }
        public static int AddBindingParametersCallCount { get; protected set; }
        public static int ApplyDispatchBehaviorCount { get; protected set; }
        public static int ValidateCallCount { get; protected set; }
        public int CallCount { get; private set; }

        public static void ClearCounts()
        {
            CreationCount = 0;
            DisposalCount = 0;
            AddBindingParametersCallCount = 0;
            ApplyDispatchBehaviorCount = 0;
            ValidateCallCount = 0;
        }

        public InstanceContextSimpleServiceBase()
        {
            CreationCount++;
        }

        public string Echo(string echo)
        {
            CallCount++;
            return echo;
        }

        protected bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
            DisposalCount++;
        }
    }
}
