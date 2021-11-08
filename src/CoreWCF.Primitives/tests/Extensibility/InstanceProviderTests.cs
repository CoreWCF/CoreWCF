// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;
using Helpers;
using Xunit;

namespace Extensibility
{
    public class InstanceProviderTests
    {
        [Fact]
        public void InstanceProviderCalledTest()
        {
            var instanceProvider = new TestInstanceProvider();
            var behavior = new TestServiceBehavior { InstanceProvider = instanceProvider };
            System.ServiceModel.ChannelFactory<ISimpleService> factory = ExtensibilityHelper.CreateChannelFactory<SimpleService, ISimpleService>(behavior);
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            string echo = channel.Echo("hello");
            Assert.Equal("hello", echo);
            instanceProvider.WaitForReleaseAsync(TimeSpan.FromSeconds(10)).Wait();
            Assert.Equal(1, instanceProvider.GetInstanceCallCount);
            Assert.Equal(1, instanceProvider.ReleaseInstanceCallCount);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public void InstanceProviderReleaseCalledWithCorrectObjectTest()
        {
            var instanceProvider = new TestInstanceProvider();
            var behavior = new TestServiceBehavior { InstanceProvider = instanceProvider };
            System.ServiceModel.ChannelFactory<ISimpleService> factory = ExtensibilityHelper.CreateChannelFactory<SimpleService, ISimpleService>(behavior);
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            string echo = channel.Echo("hello");
            Assert.Equal("hello", echo);
            instanceProvider.WaitForReleaseAsync(TimeSpan.FromSeconds(10)).Wait();
            Assert.True(instanceProvider.InstanceHashCode > 0); ;
            Assert.Equal(instanceProvider.ReleasedInstanceHashCode, instanceProvider.InstanceHashCode);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }
    }

    public class TestInstanceProvider : IInstanceProvider
    {
        public int GetInstanceCallCount { get; private set; }
        public int ReleaseInstanceCallCount { get; private set; }
        public int InstanceHashCode { get; private set; } = -1;
        public int ReleasedInstanceHashCode { get; private set; } = -2;
        private readonly SemaphoreSlim _asyncLock = new SemaphoreSlim(1);
        private bool _asyncLockAquired = false;

        public object GetInstance(InstanceContext instanceContext)
        {
            if (!_asyncLockAquired)
            {
                _asyncLock.Wait();
                _asyncLockAquired = true;
            }
            GetInstanceCallCount++;
            var service = new SimpleService();
            InstanceHashCode = service.GetHashCode();
            return service;
        }

        public object GetInstance(InstanceContext instanceContext, Message message)
        {
            if (!_asyncLockAquired)
            {
                _asyncLock.Wait();
                _asyncLockAquired = true;
            }
            GetInstanceCallCount++;
            var service = new SimpleService();
            InstanceHashCode = service.GetHashCode();
            return service;
        }

        public void ReleaseInstance(InstanceContext instanceContext, object instance)
        {
            ReleasedInstanceHashCode = instance.GetHashCode();
            ReleaseInstanceCallCount++;
            _asyncLock.Release();
            _asyncLockAquired = false;
        }

        public async Task WaitForReleaseAsync(TimeSpan timeout)
        {
            if (await _asyncLock.WaitAsync(timeout))
            {
                _asyncLock.Release();
            }
        }
    }

    public class SimpleService : ISimpleService
    {
        private readonly DateTime _dateTime;

        public SimpleService()
        {
            _dateTime = DateTime.UtcNow;
        }
        public string Echo(string echo)
        {
            return echo;
        }
    }
}
