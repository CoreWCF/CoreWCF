using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.Dispatcher;
using CoreWCF.Primitives.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.Primitives.Tests.Extensibility
{
    public class MessageInspectorTests
    {
        [Fact]
        public static void MessageInspectorCalled()
        {
            var services = new ServiceCollection();
            var inspector = new TestDispatchMessageInspector();
            var behavior = new TestServiceBehavior { DispatchMessageInspector = inspector };
            services.AddSingleton<IServiceBehavior>(behavior);
            ExtensibilityTestHelper.BuildDispatcherAndCallService(services);
            Assert.True(inspector.AfterReceiveCalled);
            Assert.True(inspector.BeforeSendCalled);
            Assert.True(inspector.CorrelationStateMatch);
        }

        [Fact]
        public static void ReplacementMessageUsed()
        {
            string replacementEchoString = "bbbbb";
            var services = new ServiceCollection();
            var inspector = new MessageReplacingDispatchMessageInspector(replacementEchoString);
            var behavior = new TestServiceBehavior { DispatchMessageInspector = inspector };
            services.AddSingleton<IServiceBehavior>(behavior);
            var service = new DispatcherTestService();
            ExtensibilityTestHelper.BuildDispatcherAndCallService(services, service);
            Assert.Equal(replacementEchoString, service.ReceivedEcho);
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
