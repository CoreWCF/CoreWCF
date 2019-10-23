using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Primitives.Tests.Helpers;
using Helpers;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Xunit;

namespace DependencyInjection
{
    public class ServiceInstanceContextModeTests
    {
        [Fact]
        public static void InstanceContextMode_Single()
        {
            SingleInstanceContextSimpleService.CreationCount = 0;
            SingleInstanceContextSimpleService.DisposalCount = 0;
            var services = new ServiceCollection();
            var serviceInstance = new SingleInstanceContextSimpleService();
            services.AddSingleton(serviceInstance);
            TestHelper.BuildDispatcherAndCallService<SingleInstanceContextSimpleService>(services);
            Assert.Equal(1, SingleInstanceContextSimpleService.CreationCount);
            Assert.Equal(0, SingleInstanceContextSimpleService.DisposalCount);
            Assert.Equal(1, serviceInstance.CallCount);
        }

        [Fact]
        public static void InstanceContextMode_Single_NoInjection()
        {
            SingleInstanceContextSimpleService.CreationCount = 0;
            SingleInstanceContextSimpleService.DisposalCount = 0;
            var services = new ServiceCollection();
            TestHelper.BuildDispatcherAndCallService<SingleInstanceContextSimpleService>(services);
            Assert.Equal(1, SingleInstanceContextSimpleService.CreationCount);
            Assert.Equal(0, SingleInstanceContextSimpleService.DisposalCount);
        }

        [Fact]
        public static void InstanceContextMode_PerCall()
        {
            PerCallInstanceContextSimpleService.CreationCount = 0;
            PerCallInstanceContextSimpleService.DisposalCount = 0;
            var services = new ServiceCollection();
            services.AddTransient<PerCallInstanceContextSimpleService>();
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<PerCallInstanceContextSimpleService>(services, serviceAddress);
            IChannel mockChannel = new MockReplyChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            // Instance created as part of service startup to probe if type is availale in DI
            Assert.Equal(1, PerCallInstanceContextSimpleService.CreationCount);
            Assert.Equal(1, PerCallInstanceContextSimpleService.DisposalCount);

            PerCallInstanceContextSimpleService.CreationCount = 0;
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            Assert.Equal(2, PerCallInstanceContextSimpleService.CreationCount);
            Assert.Equal(3, PerCallInstanceContextSimpleService.DisposalCount);
        }

        [Fact]
        public static void InstanceContextMode_PerCall_NoInjection()
        {
            PerCallInstanceContextSimpleService.CreationCount = 0;
            PerCallInstanceContextSimpleService.DisposalCount = 0;
            var services = new ServiceCollection();
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<PerCallInstanceContextSimpleService>(services, serviceAddress);
            IChannel mockChannel = new MockReplyChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            // Instance shouldn't be created as part of service startup to as type isn't availale in DI
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
        public static void InstanceContextMode_PerSession()
        {
            PerSessionInstanceContextSimpleService.CreationCount = 0;
            PerSessionInstanceContextSimpleService.DisposalCount = 0;
            var services = new ServiceCollection();
            services.AddTransient<PerSessionInstanceContextSimpleService>();
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<PerSessionInstanceContextSimpleService>(services, serviceAddress);
            IChannel mockChannel = new MockReplySessionChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            // Instance created as part of service startup to probe if type is availale in DI
            Assert.Equal(1, PerSessionInstanceContextSimpleService.CreationCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleService.DisposalCount);

            PerSessionInstanceContextSimpleService.CreationCount = 0;
            var requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            requestContext = TestRequestContext.Create(serviceAddress);
            dispatcher.DispatchAsync(requestContext, CancellationToken.None).Wait();
            // Close channel/session by sending null request context
            dispatcher.DispatchAsync(null, CancellationToken.None).Wait();
            Assert.Equal(1, PerSessionInstanceContextSimpleService.CreationCount);
            Assert.Equal(2, PerSessionInstanceContextSimpleService.DisposalCount);
        }

        [Fact]
        public static void InstanceContextMode_PerSession_NoInjection()
        {
            PerSessionInstanceContextSimpleService.CreationCount = 0;
            PerSessionInstanceContextSimpleService.DisposalCount = 0;
            var services = new ServiceCollection();
            string serviceAddress = "http://localhost/dummy";
            var serviceDispatcher = TestHelper.BuildDispatcher<PerSessionInstanceContextSimpleService>(services, serviceAddress);
            IChannel mockChannel = new MockReplySessionChannel(services.BuildServiceProvider());
            var dispatcher = serviceDispatcher.CreateServiceChannelDispatcher(mockChannel);
            // Instance shouldn't be created as part of service startup to as type isn't availale in DI
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

    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class SingleInstanceContextSimpleService : InstanceContextSimpleServiceBase<SingleInstanceContextSimpleService> { }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class PerCallInstanceContextSimpleService : InstanceContextSimpleServiceBase<PerCallInstanceContextSimpleService> { }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    public class PerSessionInstanceContextSimpleService : InstanceContextSimpleServiceBase<PerSessionInstanceContextSimpleService> { }

    public abstract class InstanceContextSimpleServiceBase<TService> : ISimpleService, IDisposable where TService : InstanceContextSimpleServiceBase<TService>
    {
        public static int CreationCount { get; set; }
        public static int DisposalCount { get; set; }
        public int CallCount { get; private set; }

        public InstanceContextSimpleServiceBase()
        {
            CreationCount++;
        }

        public string Echo(string echo)
        {
            CallCount++;
            return echo;
        }

        public void Dispose()
        {
            DisposalCount++;
        }
    }
}
