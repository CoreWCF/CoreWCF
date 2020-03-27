﻿using CoreWCF.Dispatcher;
using Helpers;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Extensibility
{
    public class OperationInvokerTests
    {
        [Fact]
        public static void OperationInvokerCalled()
        {
            TestDispatchOperationInvoker.ClearCounts();
            var behavior = new TestServiceBehavior { OperationInvokerFactory = TestDispatchOperationInvoker.Create };
            var factory = ExtensibilityHelper.CreateChannelFactory<SimpleService, ISimpleService>(behavior);
            factory.Open();
            var channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            var echo = channel.Echo("hello");
            Assert.Equal("hello", echo);
            Assert.Equal(1, TestDispatchOperationInvoker.InvokeCount);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void OperationInvokerCalledMultiple()
        {
            TestDispatchOperationInvoker.ClearCounts();
            var behavior = new TestServiceBehavior { OperationInvokerFactory = TestDispatchOperationInvoker.Create };
            var factory = ExtensibilityHelper.CreateChannelFactory<SimpleService, ISimpleService>(behavior);
            factory.Open();
            var channel = factory.CreateChannel();
            ((System.ServiceModel.Channels.IChannel)channel).Open();
            foreach (var dummy in Enumerable.Range(0, 10))
            {
                var echo = channel.Echo("hello");
                Assert.Equal("hello", echo);
            }
            Assert.Equal(10, TestDispatchOperationInvoker.InvokeCount);
            Assert.Equal(1, TestDispatchOperationInvoker.InstanceCount);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

    }

    internal class TestDispatchOperationInvoker : IOperationInvoker
    {
        public static int InstanceCount = 0;
        public static int InvokeCount = 0;

        private IOperationInvoker _innerInvoker;

        public TestDispatchOperationInvoker(IOperationInvoker innerInvoker)
        {
            _innerInvoker = innerInvoker;
            InstanceCount++;
        }

        public object[] AllocateInputs()
        {
            return _innerInvoker.AllocateInputs();
        }

        public ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
        {
            InvokeCount++;
            return _innerInvoker.InvokeAsync(instance, inputs);
        }

        public static IOperationInvoker Create(IOperationInvoker inner)
        {
            return new TestDispatchOperationInvoker(inner);
        }

        public static void ClearCounts()
        {
            InstanceCount = 0;
            InvokeCount = 0;
        }
    }
}
