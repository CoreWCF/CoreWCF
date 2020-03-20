using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using System;
using Xunit;
using CoreWCF;
using Helpers;
using CoreWCF.Runtime;
using System.Threading.Tasks;

namespace Extensibility
{
    public class InstanceProviderTests
    {
        [Fact]
        public void InstanceProviderCalledTest()
        {
            var instanceProvider = new TestInstanceProvider();
            var behavior = new TestServiceBehavior { InstanceProvider = instanceProvider };
            var factory = ExtensibilityHelper.CreateChannelFactory<SimpleService, ISimpleService>(behavior);
            factory.Open();
            var channel = factory.CreateChannel();
            var echo = channel.Echo("hello");
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
            var factory = ExtensibilityHelper.CreateChannelFactory<SimpleService, ISimpleService>(behavior);
            factory.Open();
            var channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            var echo = channel.Echo("hello");
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
        private AsyncLock _asyncLock = new AsyncLock();
        private IDisposable _asyncLockHoldObj = null;

        public object GetInstance(InstanceContext instanceContext)
        {
            if (_asyncLockHoldObj == null)
            {
                _asyncLockHoldObj = _asyncLock.TakeLock();
            }
            GetInstanceCallCount++;
            var service = new SimpleService();
            InstanceHashCode = service.GetHashCode();
            return service;
        }

        public object GetInstance(InstanceContext instanceContext, Message message)
        {
            if (_asyncLockHoldObj == null)
            {
                _asyncLockHoldObj = _asyncLock.TakeLock();
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
            _asyncLockHoldObj?.Dispose();
            _asyncLockHoldObj = null;
        }

        public async Task WaitForReleaseAsync(TimeSpan timeout)
        {
            (await _asyncLock.TakeLockAsync(timeout))?.Dispose();
        }
    }

    public class SimpleService : ISimpleService
    {
        private DateTime _dateTime;

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
