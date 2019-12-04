using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Extensibility
{
    public class MessageInspectorTests
    {
        [Fact]
        public static void MessageInspectorCalled()
        {
            var inspector = new TestDispatchMessageInspector();
            var behavior = new TestServiceBehavior { DispatchMessageInspector = inspector };
            var factory = ExtensibilityHelper.CreateChannelFactory<SimpleService, ISimpleService>(behavior);
            factory.Open();
            var channel = factory.CreateChannel();
            var echo = channel.Echo("hello");
            Assert.Equal("hello", echo);
            Assert.True(inspector.AfterReceiveCalled);
            Assert.True(inspector.BeforeSendCalled);
            Assert.True(inspector.CorrelationStateMatch);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }

        [Fact]
        public static void ReplacementMessageUsed()
        {
            string replacementEchoString = "bbbbb";
            var inspector = new MessageReplacingDispatchMessageInspector(replacementEchoString);
            var behavior = new TestServiceBehavior { DispatchMessageInspector = inspector };
            var service = new DispatcherTestService();
            var factory = ExtensibilityHelper.CreateChannelFactory<DispatcherTestService, ISimpleService>(
                (services) =>
                {
                    services.AddSingleton<IServiceBehavior>(behavior);
                    services.AddSingleton(service);
                });
            factory.Open();
            var channel = factory.CreateChannel();
            var echo = channel.Echo("hello");
            Assert.Equal(replacementEchoString, service.ReceivedEcho);
            Assert.Equal(replacementEchoString, echo);
            ((System.ServiceModel.Channels.IChannel)channel).Close();
            factory.Close();
            TestHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
        }
    }

    public class TestDispatchMessageInspector : IDispatchMessageInspector
    {
        public bool AfterReceiveCalled { get; private set; }
        public bool BeforeSendCalled { get; private set; }
        public bool CorrelationStateMatch { get; private set; }

        private object _correlationState;

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            AfterReceiveCalled = true;
            _correlationState = new object();
            return _correlationState;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            BeforeSendCalled = true;
            CorrelationStateMatch = ReferenceEquals(correlationState, _correlationState);
        }
    }

    public class MessageReplacingDispatchMessageInspector : IDispatchMessageInspector
    {
        private string _replacementEchoString;

        public MessageReplacingDispatchMessageInspector(string replacementEchoString)
        {
            _replacementEchoString = replacementEchoString;
        }

        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            var requestMessage = TestHelper.CreateEchoRequestMessage(_replacementEchoString);
            requestMessage.Headers.To = request.Headers.To;
            request = requestMessage;
            return null;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            return;
        }
    }

    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class DispatcherTestService : ISimpleService
    {
        public string ReceivedEcho { get; private set; }
        public string Echo(string echo)
        {
            ReceivedEcho = echo;
            return echo;
        }
    }
}
