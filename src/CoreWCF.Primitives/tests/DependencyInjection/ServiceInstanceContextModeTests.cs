// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using DispatcherClient;
using Extensibility;
using Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DependencyInjection
{
    public class ServiceInstanceContextModeTests
    {
        [Fact]
        public static void InstanceContextMode_Single()
        {
            SingleInstanceContextSimpleService.ClearCounts();
            var serviceInstance = new SingleInstanceContextSimpleService();
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<SingleInstanceContextSimpleService, ISimpleService>(
                (services) =>
                {
                    services.AddSingleton(serviceInstance);
                });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            Assert.Equal(1, SingleInstanceContextSimpleService.AddBindingParametersCallCount);
            Assert.Equal(1, SingleInstanceContextSimpleService.ApplyDispatchBehaviorCount);
            Assert.Equal(1, SingleInstanceContextSimpleService.ValidateCallCount);
            string echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            Assert.Equal(1, SingleInstanceContextSimpleService.CreationCount);
            Assert.Equal(0, SingleInstanceContextSimpleService.DisposalCount);
            Assert.Equal(2, serviceInstance.CallCount);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void InstanceContextMode_Single_NoInjection()
        {
            SingleInstanceContextSimpleService.ClearCounts();
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<SingleInstanceContextSimpleService, ISimpleService>();
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            Assert.Equal(1, SingleInstanceContextSimpleService.AddBindingParametersCallCount);
            Assert.Equal(1, SingleInstanceContextSimpleService.ApplyDispatchBehaviorCount);
            Assert.Equal(1, SingleInstanceContextSimpleService.ValidateCallCount);
            string echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            Assert.Equal(1, SingleInstanceContextSimpleService.CreationCount);
            Assert.Equal(0, SingleInstanceContextSimpleService.DisposalCount);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void InstanceContextMode_PerCall()
        {
            PerCallInstanceContextSimpleServiceAndBehavior.ClearCounts();
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<PerCallInstanceContextSimpleServiceAndBehavior, ISimpleService>(
                (services) =>
                {
                    services.AddTransient<PerCallInstanceContextSimpleServiceAndBehavior>();
                });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            // Instance created as part of service startup to probe if type is availale in DI
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.CreationCount);
            // Instance not disposed as it implements IServiceBehavior and is added to service behaviors
            Assert.Equal(0, PerCallInstanceContextSimpleServiceAndBehavior.DisposalCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.AddBindingParametersCallCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.ApplyDispatchBehaviorCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.ValidateCallCount);

            PerCallInstanceContextSimpleServiceAndBehavior.ClearCounts();
            string echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            PerCallInstanceContextSimpleServiceAndBehavior.WaitForDisposalCount(2, TimeSpan.FromSeconds(30));
            Assert.Equal(2, PerCallInstanceContextSimpleServiceAndBehavior.CreationCount);
            Assert.Equal(2, PerCallInstanceContextSimpleServiceAndBehavior.DisposalCount);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void InstanceContextMode_PerCall_WithScopedCtorDependency()
        {
            ScopedCtorDependency.ClearCounts();
            PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.ClearCounts();
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency, ISimpleService>(
                (services) =>
                {
                    services.AddScoped<IScopedCtorDependency, ScopedCtorDependency>();
                    services.AddTransient<PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency>();
                });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            // Instance created as part of service startup to probe if type is availale in DI
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.CreationCount);
            // Instance not disposed as it implements IServiceBehavior and is added to service behaviors
            Assert.Equal(0, PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.DisposalCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.AddBindingParametersCallCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.ApplyDispatchBehaviorCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.ValidateCallCount);

            PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.ClearCounts();
            ScopedCtorDependency.ClearCounts();

            string echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.WaitForDisposalCount(2, TimeSpan.FromSeconds(30));
            Assert.Equal(2, PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.CreationCount);
            Assert.Equal(2, PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.DisposalCount);

            ScopedCtorDependency.WaitForDisposalCount(2, TimeSpan.FromSeconds(30));
            Assert.Equal(2, ScopedCtorDependency.CreationCount);
            Assert.Equal(2, ScopedCtorDependency.DisposalCount);

            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void InstanceContextMode_PerCall_NoInjection()
        {
            PerCallInstanceContextSimpleService.ClearCounts();
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<PerCallInstanceContextSimpleService, ISimpleService>();
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            // Instance shouldn't be created as part of service startup as type isn't available in DI
            Assert.Equal(0, PerCallInstanceContextSimpleService.CreationCount);
            Assert.Equal(0, PerCallInstanceContextSimpleService.DisposalCount);

            string echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            PerCallInstanceContextSimpleService.WaitForDisposalCount(2, TimeSpan.FromSeconds(30));
            Assert.Equal(2, PerCallInstanceContextSimpleService.CreationCount);
            Assert.Equal(2, PerCallInstanceContextSimpleService.DisposalCount);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void InstanceContextMode_PerCall_WithBehavior_NoInjection()
        {
            PerCallInstanceContextSimpleServiceAndBehavior.ClearCounts();
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<PerCallInstanceContextSimpleServiceAndBehavior, ISimpleService>();
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            // Instance created as part of service startup as it implements IServiceBehavior
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.CreationCount);
            // Instance not disposed as it implements IServiceBehavior and is added to service behaviors
            Assert.Equal(0, PerCallInstanceContextSimpleServiceAndBehavior.DisposalCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.AddBindingParametersCallCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.ApplyDispatchBehaviorCount);
            Assert.Equal(1, PerCallInstanceContextSimpleServiceAndBehavior.ValidateCallCount);

            PerCallInstanceContextSimpleServiceAndBehavior.ClearCounts();
            string echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            PerCallInstanceContextSimpleServiceAndBehavior.WaitForDisposalCount(2, TimeSpan.FromSeconds(30));
            Assert.Equal(2, PerCallInstanceContextSimpleServiceAndBehavior.CreationCount);
            Assert.Equal(2, PerCallInstanceContextSimpleServiceAndBehavior.DisposalCount);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void InstanceContextMode_PerSession()
        {
            PerSessionInstanceContextSimpleServiceAndBehavior.ClearCounts();
            System.ServiceModel.ChannelFactory<ISimpleSessionService> factory = DispatcherHelper.CreateChannelFactory<PerSessionInstanceContextSimpleServiceAndBehavior, ISimpleSessionService>(
                (services) =>
                {
                    services.AddTransient<PerSessionInstanceContextSimpleServiceAndBehavior>();
                });
            factory.Open();
            ISimpleSessionService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            // Instance created as part of service startup to probe if type is available in DI
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.CreationCount);
            // Instance not disposed as it implements IServiceBehavior and is added to service behaviors
            Assert.Equal(0, PerSessionInstanceContextSimpleServiceAndBehavior.DisposalCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.AddBindingParametersCallCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.ApplyDispatchBehaviorCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.ValidateCallCount);

            PerSessionInstanceContextSimpleServiceAndBehavior.ClearCounts();
            string echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.CreationCount);
            PerSessionInstanceContextSimpleServiceAndBehavior.WaitForDisposalCount(1, TimeSpan.FromSeconds(30));
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.DisposalCount);
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void InstanceContextMode_PerSession_WithScopedCtorDependency()
        {
            PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.ClearCounts();
            System.ServiceModel.ChannelFactory<ISimpleSessionService> factory = DispatcherHelper.CreateChannelFactory<PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency, ISimpleSessionService>(
                (services) =>
                {
                    services.AddScoped<IScopedCtorDependency, ScopedCtorDependency>();
                    services.AddTransient<PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency>();
                });
            factory.Open();
            ISimpleSessionService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            // Instance created as part of service startup to probe if type is available in DI
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.CreationCount);
            // Instance not disposed as it implements IServiceBehavior and is added to service behaviors
            Assert.Equal(0, PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.DisposalCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.AddBindingParametersCallCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.ApplyDispatchBehaviorCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.ValidateCallCount);

            PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.ClearCounts();
            ScopedCtorDependency.ClearCounts();

            string echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            ((System.ServiceModel.Channels.IChannel)channel).Close();

            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.CreationCount);
            PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.WaitForDisposalCount(1, TimeSpan.FromSeconds(30));
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency.DisposalCount);

            ScopedCtorDependency.WaitForDisposalCount(1, TimeSpan.FromSeconds(30));
            Assert.Equal(1, ScopedCtorDependency.CreationCount);
            Assert.Equal(1, ScopedCtorDependency.DisposalCount);

            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void InstanceContextMode_PerSession_NoInjection()
        {
            PerSessionInstanceContextSimpleService.ClearCounts();
            System.ServiceModel.ChannelFactory<ISimpleSessionService> factory = DispatcherHelper.CreateChannelFactory<PerSessionInstanceContextSimpleService, ISimpleSessionService>();
            factory.Open();
            ISimpleSessionService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            // Instance shouldn't be created as part of service startup to as type isn't available in DI
            Assert.Equal(0, PerSessionInstanceContextSimpleService.CreationCount);
            Assert.Equal(0, PerSessionInstanceContextSimpleService.DisposalCount);

            PerSessionInstanceContextSimpleService.ClearCounts();
            string echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            Assert.Equal(1, PerSessionInstanceContextSimpleService.CreationCount);
            PerSessionInstanceContextSimpleService.WaitForDisposalCount(1, TimeSpan.FromSeconds(30));
            Assert.Equal(1, PerSessionInstanceContextSimpleService.DisposalCount);
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void InstanceContextMode_PerSession_WithBehavior_NoInjection()
        {
            PerSessionInstanceContextSimpleServiceAndBehavior.ClearCounts();
            System.ServiceModel.ChannelFactory<ISimpleSessionService> factory = DispatcherHelper.CreateChannelFactory<PerSessionInstanceContextSimpleServiceAndBehavior, ISimpleSessionService>();
            factory.Open();
            ISimpleSessionService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            // Instance created as part of service startup as it implements IServiceBehavior
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.CreationCount);
            // Instance not disposed as it implements IServiceBehavior and is added to service behaviors
            Assert.Equal(0, PerSessionInstanceContextSimpleServiceAndBehavior.DisposalCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.AddBindingParametersCallCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.ApplyDispatchBehaviorCount);
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.ValidateCallCount);

            PerSessionInstanceContextSimpleServiceAndBehavior.ClearCounts();
            string echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            echo = channel.Echo("hello");
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.CreationCount);
            PerSessionInstanceContextSimpleServiceAndBehavior.WaitForDisposalCount(1, TimeSpan.FromSeconds(30));
            Assert.Equal(1, PerSessionInstanceContextSimpleServiceAndBehavior.DisposalCount);
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }
    }

    public interface IScopedCtorDependency { }

    public class ScopedCtorDependency : IScopedCtorDependency, IDisposable
    {
        private static int _creationCount = 0;
        private static int _disposalCount = 0;
        private static readonly ManualResetEventSlim s_disposalCountWaitable = new ManualResetEventSlim(false);

        public static int CreationCount => _creationCount;
        public static int DisposalCount => _disposalCount;

        public ScopedCtorDependency()
        {
            Interlocked.Increment(ref _creationCount);
        }

        public static void ClearCounts()
        {
            Interlocked.Exchange(ref _creationCount, 0);
            Interlocked.Exchange(ref _disposalCount, 0);
            s_disposalCountWaitable.Reset();
        }

        public void Dispose()
        {
            Interlocked.Increment(ref _disposalCount);
            s_disposalCountWaitable.Set();
        }

        public static void WaitForDisposalCount(int expectedDisposals, TimeSpan maxWait)
        {
            DateTime maxWaitDeadline = DateTime.Now + maxWait;
            while (DateTime.Now < maxWaitDeadline && expectedDisposals > DisposalCount)
            {
                // There's a small race condition here where DisposalCount could be incremented and the MRE set
                // before we call reset. In which case we'll wait maxWait time and then the test will pass. The
                // delay shouldn't be more than a few seconds anyway so this won't have any significant impact and
                // it has no affect on the pass/fail of the test
                s_disposalCountWaitable.Reset();
                s_disposalCountWaitable.Wait(maxWaitDeadline - DateTime.Now);
            }
        }
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class SingleInstanceContextSimpleService : InstanceContextSimpleServiceAndBehaviorBase<SingleInstanceContextSimpleService> { }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class PerCallInstanceContextSimpleService : InstanceContextSimpleServiceBase<PerCallInstanceContextSimpleService> { }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class PerCallInstanceContextSimpleServiceAndBehavior : InstanceContextSimpleServiceAndBehaviorBase<PerCallInstanceContextSimpleServiceAndBehavior> { }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerCall)]
    public class PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency : InstanceContextSimpleServiceAndBehaviorBase<PerCallInstanceContextSimpleServiceAndBehavior>
    {
        private readonly IScopedCtorDependency _scopedCtorDependency;

        public PerCallInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency(IScopedCtorDependency scopedCtorDependency)
        {
            _scopedCtorDependency = scopedCtorDependency;
        }
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    public class PerSessionInstanceContextSimpleService : InstanceContextSimpleServiceBase<PerSessionInstanceContextSimpleService>, ISimpleSessionService { }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    public class PerSessionInstanceContextSimpleServiceAndBehavior : InstanceContextSimpleServiceAndBehaviorBase<PerSessionInstanceContextSimpleServiceAndBehavior>, ISimpleSessionService { }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.PerSession)]
    public class PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency : InstanceContextSimpleServiceAndBehaviorBase<PerSessionInstanceContextSimpleServiceAndBehavior>, ISimpleSessionService
    {
        private readonly IScopedCtorDependency _scopedCtorDependency;

        public PerSessionInstanceContextSimpleServiceAndBehaviorWithScopedCtorDependency(IScopedCtorDependency scopedCtorDependency)
        {
            _scopedCtorDependency = scopedCtorDependency;
        }
    }

    public abstract class InstanceContextSimpleServiceAndBehaviorBase<TService> : InstanceContextSimpleServiceBase<TService>, IServiceBehavior where TService : InstanceContextSimpleServiceAndBehaviorBase<TService>
    {

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            AddBindingParametersCallCount++;
        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

            ApplyDispatchBehaviorCount++;
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }

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
        private static readonly ManualResetEventSlim s_disposalCountWaitable = new ManualResetEventSlim(false);

        public static void ClearCounts()
        {
            CreationCount = 0;
            DisposalCount = 0;
            AddBindingParametersCallCount = 0;
            ApplyDispatchBehaviorCount = 0;
            ValidateCallCount = 0;
            s_disposalCountWaitable.Reset();
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
            s_disposalCountWaitable.Set();
        }

        public static void WaitForDisposalCount(int expectedDisposals, TimeSpan maxWait)
        {
            DateTime maxWaitDeadline = DateTime.Now + maxWait;
            while (DateTime.Now < maxWaitDeadline && expectedDisposals > DisposalCount)
            {
                // There's a small race condition here where DisposalCount could be incremented and the MRE set
                // before we call reset. In which case we'll wait maxWait time and then the test will pass. The
                // delay shouldn't be more than a few seconds anyway so this won't have any significant impact and
                // it has no affect on the pass/fail of the test
                s_disposalCountWaitable.Reset();
                s_disposalCountWaitable.Wait(maxWaitDeadline - DateTime.Now);
            }
        }
    }
}
