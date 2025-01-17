// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Queue.Common;
using CoreWCF.Queue.Common.Configuration;
using CoreWCF.Queue.Tests.Fakes;
using Xunit;

namespace CoreWCF.Queue.Tests
{
    public class DefaultQueueTransportPumpTests
    {
        [Fact]
        public async Task DefaultQueueTransportPump_WhenStartedAndStopped_TransportReturnsSuccess()
        {
            var transport = new FakeQueueTransport(CallType.Success);
            var messageReceiver = QueueTransportPump.CreateDefaultPump(transport);
            int handshakeCallCount = 0;
            QueueMessageDispatcherDelegate messageDispatcher = _ =>
            {
                Interlocked.Increment(ref handshakeCallCount);
                return Task.CompletedTask;
            };

            var transportContext = new QueueTransportContext(
                new FakeServiceDispatcher(),
                null,
                new FakeBindingElement(),
                messageDispatcher,
                messageReceiver);
            await messageReceiver.StartPumpAsync(transportContext, default);
            await Task.Delay(100);
            await messageReceiver.StopPumpAsync(default);

            Assert.True(transport.CallCount > 1);
            Assert.True(handshakeCallCount > 1);
        }

        [Fact]
        public async Task DefaultQueueTransportPump_WhenStarted_TransportDoesNotReturnNull()
        {
            var transport = new FakeQueueTransport(CallType.ReturnNull);
            var messageReceiver = QueueTransportPump.CreateDefaultPump(transport);
            int handshakeCallCount = 0;
            QueueMessageDispatcherDelegate messageDispatcher = _ =>
            {
                handshakeCallCount++;
                return Task.CompletedTask;
            };

            var transportContext = new QueueTransportContext(
                new FakeServiceDispatcher(),
                null,
                new FakeBindingElement(),
                messageDispatcher,
                messageReceiver);
            await messageReceiver.StartPumpAsync(transportContext, default);
            await Task.Delay(100);
            await messageReceiver.StopPumpAsync(default);

            Assert.True(transport.CallCount > 1, $"CallCount should have been > 1, but was {transport.CallCount}");
            Assert.Equal(0, handshakeCallCount);
        }

        [Fact]
        public async Task DefaultQueueTransportPump_WhenStarted_TransportDoesNotThrowException()
        {
            var transport = new FakeQueueTransport(CallType.ThrowException);
            var messageReceiver = QueueTransportPump.CreateDefaultPump(transport);
            int handshakeCallCount = 0;
            QueueMessageDispatcherDelegate messageDispatcher = _ =>
            {
                handshakeCallCount++;
                return Task.CompletedTask;
            };

            var transportContext = new QueueTransportContext(
                new FakeServiceDispatcher(),
                null,
                new FakeBindingElement(),
                messageDispatcher,
                messageReceiver);
            await messageReceiver.StartPumpAsync(transportContext, default);
            await Task.Delay(100);
            await messageReceiver.StopPumpAsync(default);

            Assert.True(transport.CallCount > 1, $"CallCount should have been > 1, but was {transport.CallCount}");
            Assert.Equal(0, handshakeCallCount);
        }
    }
}
