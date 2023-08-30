// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using Extensibility;
using Helpers;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Primitives.Tests
{
    public class MessagePropertyAttributeTests
    {
        private readonly ITestOutputHelper _output;

        public MessagePropertyAttributeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ValidateMessagePropertyPopulated()
        {
            var inspector = new PropertyCheckingDispatchMessageInspector(SimpleResponse.PropertyName);
            var behavior = new TestServiceBehavior { DispatchMessageInspector = inspector };
            System.ServiceModel.ChannelFactory<IMessagePropertyService> factory = ExtensibilityHelper.CreateChannelFactory<MessagePropertyService, IMessagePropertyService>(behavior);
            factory.Open();
            IMessagePropertyService channel = factory.CreateChannel();
            var testString = Guid.NewGuid().ToString();
            var response = channel.Request(new SimpleRequest { stringParam = testString });
            Assert.Equal(testString, response.stringParam);
            Assert.Null(response.stringProperty);
            Assert.NotNull(inspector.MessagePropertyValue);
            Assert.IsType<string>(inspector.MessagePropertyValue);
            Assert.Equal(testString, inspector.MessagePropertyValue as string);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        public class MessagePropertyService : IMessagePropertyService
        {
            public SimpleResponse Request(SimpleRequest request)
            {
                return new SimpleResponse
                {
                    stringParam = request.stringParam,
                    stringProperty = request.stringParam
                };
            }
        }

        [System.ServiceModel.ServiceContract]
        internal interface IMessagePropertyService
        {
            [System.ServiceModel.OperationContract]
            SimpleResponse Request(SimpleRequest request);
        }

        [System.ServiceModel.MessageContract(WrapperName = "MPRequest", WrapperNamespace = "http://tempuri.org")]
        public class SimpleRequest
        {
            [System.ServiceModel.MessageBodyMember]
            public string stringParam;
        }

        [System.ServiceModel.MessageContract(WrapperName = "MPResponse", WrapperNamespace = "http://tempuri.org")]
        public class SimpleResponse
        {
            public const string PropertyName = "MPMessageProperty";
            [System.ServiceModel.MessageBodyMember]
            public string stringParam;
            [System.ServiceModel.MessageProperty(Name = PropertyName)]
            public string stringProperty;
        }

        internal class PropertyCheckingDispatchMessageInspector : IDispatchMessageInspector
        {
            private string _propertyName;

            public PropertyCheckingDispatchMessageInspector(string propertyName)
            {
                _propertyName = propertyName;
            }

            public object MessagePropertyValue { get; private set; }

            public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
            {
                return null;
            }

            public void BeforeSendReply(ref Message reply, object correlationState)
            {
                if (reply.Properties.ContainsKey(_propertyName))
                {
                    MessagePropertyValue = reply.Properties[_propertyName];
                }
            }
        }
    }
}
