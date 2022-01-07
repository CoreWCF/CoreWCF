// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using Helpers;
using Xunit;

namespace Extensibility
{
    public class CallContextInitializersTests
    {
        [Fact]
        public static void CallContextInitializersCalled()
        {
            var callContextInitializer = new TestCallContextInitializers();
            var endpointBehavior = new TestEndpointBehavior(callContextInitializer);

            var factory = ConfigureFactory(endpointBehavior);
            ISimpleService channel = factory.CreateChannel();
            string echo = channel.Echo("hello");
            Assert.Equal("hello", echo);

            Assert.Equal(1, callContextInitializer.BeforeInvokeCount);
            Assert.Equal(1, callContextInitializer.AfterInvokeCount);

            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void CallContextInitializersCalledMulitple()
        {
            const int timesToCall = 10;
            var callContextInitializer = new TestCallContextInitializers();
            var endpointBehavior = new TestEndpointBehavior(callContextInitializer);

            var factory = ConfigureFactory(endpointBehavior);
            ISimpleService channel = factory.CreateChannel();
            foreach (int dummy in Enumerable.Range(0, timesToCall))
            {
                string echo = channel.Echo("hello");
                Assert.Equal("hello", echo);
            }

            Assert.Equal(timesToCall, callContextInitializer.BeforeInvokeCount);
            Assert.Equal(timesToCall, callContextInitializer.AfterInvokeCount);

            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void CorrelationStatePassedToAfterInvoke()
        {
            var expectedCorrelationState = new object();
            var callContextInitializer = new TestCallContextInitializers(expectedCorrelationState);
            var endpointBehavior = new TestEndpointBehavior(callContextInitializer);

            var factory = ConfigureFactory(endpointBehavior);
            ISimpleService channel = factory.CreateChannel();
            string echo = channel.Echo("hello");

            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);

            Assert.Same(expectedCorrelationState, callContextInitializer.ActualCorrelationState);
        }

        private static System.ServiceModel.ChannelFactory<ISimpleService> ConfigureFactory(IEndpointBehavior endpointBehavior)
        {
            System.ServiceModel.ChannelFactory<ISimpleService> factory = ExtensibilityHelper.CreateChannelFactory<SimpleService, ISimpleService>((CoreWCF.ServiceHostBase serviceHostBase) =>
            {
                foreach (var endpoint in serviceHostBase.Description.Endpoints)
                {
                    endpoint.EndpointBehaviors.Add(endpointBehavior);
                }
            });
            factory.Open();

            return factory;
        }
    }

    internal class TestEndpointBehavior : IEndpointBehavior
    {
        private readonly ICallContextInitializer _callContextInitializer;

        public TestEndpointBehavior(ICallContextInitializer callContextInitializer)
        {
            _callContextInitializer = callContextInitializer;
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters) { }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime) { }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
            foreach (DispatchOperation op in endpointDispatcher.DispatchRuntime.Operations)
            {
                op.CallContextInitializers.Add(_callContextInitializer);
            }
        }

        public void Validate(ServiceEndpoint endpoint) { }
    }

    internal class TestCallContextInitializers : ICallContextInitializer
    {
        private readonly object _correlationStateToReturn;

        public object ActualCorrelationState;
        public int AfterInvokeCount;
        public int BeforeInvokeCount;

        public TestCallContextInitializers(object correlationStateToReturn = null)
        {
            _correlationStateToReturn = correlationStateToReturn;
        }

        public void AfterInvoke(object correlationState)
        {
            ActualCorrelationState = correlationState;
            AfterInvokeCount++;
        }

        public object BeforeInvoke(InstanceContext instanceContext, IClientChannel channel, Message message)
        {
            BeforeInvokeCount++;
            return _correlationStateToReturn;
        }
    }
}
