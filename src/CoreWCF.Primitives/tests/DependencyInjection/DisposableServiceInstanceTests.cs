// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using CoreWCF.Channels;
using CoreWCF.Description;
using DispatcherClient;
using Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DependencyInjection
{
    public class DisposableServiceInstanceTests
    {
        [Fact]
        public static void InjectedSingletonInstanceWithServiceBehaviorSingle_NotDisposed()
        {
            DisposableSimpleService.InstantiationCount = 0;
            var serviceInstance = new DisposableSimpleServiceWithServiceBehaviorSingle();
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<DisposableSimpleServiceWithServiceBehaviorSingle, ISimpleService>(
                (services) =>
                {
                    services.AddSingleton(serviceInstance);
                });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            string echo = channel.Echo("hello");
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            Assert.False(serviceInstance.IsDisposed, "Service instance shouldn't be disposed");
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
            Assert.Equal(1, DisposableSimpleService.InstantiationCount);
        }

        [Fact]
        public static void InjectedTransientInstanceInjectedServiceBehaviorPerCall_Succeeds()
        {
            DisposableSimpleService.InstantiationCount = 0;
            var serviceBehaviorAttr = new CoreWCF.ServiceBehaviorAttribute();
            serviceBehaviorAttr.InstanceContextMode = CoreWCF.InstanceContextMode.PerCall;
            serviceBehaviorAttr.ConcurrencyMode = CoreWCF.ConcurrencyMode.Multiple;
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<DisposableSimpleService, ISimpleService>(
                (services) =>
                {
                    services.AddTransient<DisposableSimpleService>();
                    services.AddSingleton<IServiceBehavior>(serviceBehaviorAttr);
                });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            string echo = channel.Echo("hello");
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
            Assert.Equal(1, DisposableSimpleService.InstantiationCount);
        }

        [Fact]
        public static void InjectedTransientInstanceWithServiceBehaviorPerCall_Succeeds()
        {
            DisposableSimpleService.InstantiationCount = 0;
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<DisposableSimpleServiceWithServiceBehaviorPerCall, ISimpleService>(
                (services) =>
                {
                    services.AddTransient<DisposableSimpleServiceWithServiceBehaviorPerCall>();
                });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            string echo = channel.Echo("hello");
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
            Assert.Equal(1, DisposableSimpleService.InstantiationCount);
        }

        [Fact]
        public static void InjectedTransientInstanceIsServiceBehaviorPerCall_Succeeds()
        {
            DisposableSimpleService.InstantiationCount = 0;
            System.ServiceModel.ChannelFactory<ISimpleService> factory = DispatcherHelper.CreateChannelFactory<DisposableSimpleServiceIsServiceBehavior, ISimpleService>(
                (services) =>
                {
                    services.AddTransient<DisposableSimpleServiceIsServiceBehavior>();
                });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            string echo = channel.Echo("hello");
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
            Assert.Equal(2, DisposableSimpleService.InstantiationCount);
            Assert.True(DisposableSimpleServiceIsServiceBehavior.HasBehaviorBeenUsed);
        }

    }

    public class DisposableSimpleService : ISimpleService, IDisposable
    {
        private static int s_instantiationCount = 0;
        public static int InstantiationCount { get => s_instantiationCount; set => s_instantiationCount = value; }

        public DisposableSimpleService()
        {
            Interlocked.Increment(ref s_instantiationCount);
        }

        public string Echo(string echo)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(DisposableSimpleService));
            return echo;
        }

        public void Dispose() => IsDisposed = true;
        public bool IsDisposed { get; private set; }
    }

    [CoreWCF.ServiceBehavior(ConcurrencyMode = CoreWCF.ConcurrencyMode.Multiple, InstanceContextMode = CoreWCF.InstanceContextMode.PerCall)]
    public class DisposableSimpleServiceIsServiceBehavior : DisposableSimpleService, CoreWCF.Description.IServiceBehavior
    {
        private static bool s_addBindingParametersCalled;
        private static bool s_applyDispatchBehaviorCalled;
        private static bool s_validateCalled;

        public static bool HasBehaviorBeenUsed => s_addBindingParametersCalled && s_applyDispatchBehaviorCalled && s_validateCalled;

        public void AddBindingParameters(ServiceDescription serviceDescription, CoreWCF.ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters) => s_addBindingParametersCalled = true;
        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, CoreWCF.ServiceHostBase serviceHostBase) => s_applyDispatchBehaviorCalled = true;
        public void Validate(ServiceDescription serviceDescription, CoreWCF.ServiceHostBase serviceHostBase) => s_validateCalled = true;
    }

    [CoreWCF.ServiceBehavior(ConcurrencyMode = CoreWCF.ConcurrencyMode.Multiple, InstanceContextMode = CoreWCF.InstanceContextMode.Single)]
    public class DisposableSimpleServiceWithServiceBehaviorSingle : DisposableSimpleService
    {

    }

    [CoreWCF.ServiceBehavior(ConcurrencyMode = CoreWCF.ConcurrencyMode.Multiple, InstanceContextMode = CoreWCF.InstanceContextMode.PerCall)]
    public class DisposableSimpleServiceWithServiceBehaviorPerCall : DisposableSimpleService
    {

    }

}
