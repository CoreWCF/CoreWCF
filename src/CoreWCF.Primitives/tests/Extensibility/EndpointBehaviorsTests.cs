// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using Helpers;
using Xunit;

namespace Extensibility
{
    public class EndpointBehaviorsTests
    {
        [Fact]
        public static void EndpointBehaviorUsed()
        {
            var endpointBehavior = new TestEndpointBehavior();
            System.ServiceModel.ChannelFactory<ISimpleService> factory = ExtensibilityHelper.CreateChannelFactory<SimpleService, ISimpleService>((CoreWCF.ServiceHostBase serviceHostBase) =>
            {
                foreach(var endpoint in serviceHostBase.Description.Endpoints)
                {
                    endpoint.EndpointBehaviors.Add(endpointBehavior);
                }
            });
            factory.Open();
            ISimpleService channel = factory.CreateChannel();
            string echo = channel.Echo("hello");
            Assert.Equal("hello", echo);
            Assert.True(endpointBehavior.AddBindingParametersCalled);
            Assert.False(endpointBehavior.ApplyClientBehaviorCalled);
            Assert.True(endpointBehavior.ApplyDispatchBehaviorCalled);
            Assert.True(endpointBehavior.ValidateCalled);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        private class TestEndpointBehavior : IEndpointBehavior
        {
            public bool AddBindingParametersCalled { get; private set; }
            public bool ApplyClientBehaviorCalled { get; private set; }
            public bool ApplyDispatchBehaviorCalled { get; private set; }
            public bool ValidateCalled { get; private set; }

            public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters) => AddBindingParametersCalled = true;
            public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime) => ApplyClientBehaviorCalled = true;
            public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher) => ApplyDispatchBehaviorCalled = true;
            public void Validate(ServiceEndpoint endpoint) => ValidateCalled = true;
        }
    }
}
