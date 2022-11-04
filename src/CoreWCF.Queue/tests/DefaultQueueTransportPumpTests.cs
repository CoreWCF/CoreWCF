// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Queue.Common;
using CoreWCF.Queue.Tests.Fakes;
using Xunit;

namespace CoreWCF.Queue.Tests
{
    public class DefaultQueueTransportPumpTests
    {
        [Fact]
        public async Task DefaultQueueTransportPump_WhenStated_Success()
        {
            var transports = new FakeQueueTransport(CallType.Success);
            var messageReceiver = QueueTransportPump.CreateDefaultPump(transports);
            int handshakeCallCount = 0;

            var transportContext = new QueueTransportContext
            {
                QueueBindingElement = new FakeBindingElement(),
                ServiceDispatcher = new FakeServiceDispatcher(),
                QueueMessageDispatcher = _ =>
                {
                    handshakeCallCount++;
                    return Task.CompletedTask;
                },
            };
            await messageReceiver.StartPumpAsync(transportContext, default);
            await Task.Delay(100);
            await messageReceiver.StopPumpAsync(default);

            Assert.True(transports.CallCount > 1);
            Assert.True(handshakeCallCount > 1);
        }

        [Fact]
        public async Task DefaultQueueTransportPump_WhenStatedAndTransportReturnNull_Success()
        {
            var transports = new FakeQueueTransport(CallType.ReturnNull);
            var messageReceiver = QueueTransportPump.CreateDefaultPump(transports);
            int handshakeCallCount = 0;

            var transportContext = new QueueTransportContext
            {
                QueueBindingElement = new FakeBindingElement(),
                ServiceDispatcher = new FakeServiceDispatcher(),
                QueueMessageDispatcher = _ =>
                {
                    handshakeCallCount++;
                    return Task.CompletedTask;
                },
            };
            await messageReceiver.StartPumpAsync(transportContext, default);
            await Task.Delay(100);
            await messageReceiver.StopPumpAsync(default);

            Assert.True(transports.CallCount > 1);
            Assert.True(handshakeCallCount == 0);
        }

        [Fact]
        public async Task DefaultQueueTransportPump_WhenStatedAndTransportThrowException()
        {
            var transports = new FakeQueueTransport(CallType.ThrowException);
            var messageReceiver = QueueTransportPump.CreateDefaultPump(transports);
            int handshakeCallCount = 0;

            var transportContext = new QueueTransportContext
            {
                QueueBindingElement = new FakeBindingElement(),
                ServiceDispatcher = new FakeServiceDispatcher(),
                QueueMessageDispatcher = _ =>
                {
                    handshakeCallCount++;
                    return Task.CompletedTask;
                },
            };
            await messageReceiver.StartPumpAsync(transportContext, default);
            await Task.Delay(100);
            await messageReceiver.StopPumpAsync(default);

            Assert.True(transports.CallCount > 1);
            Assert.True(handshakeCallCount == 0);
        }

        [Fact]
        public async Task DefaultQueueTransportPump_WhenStopped_Success()
        {
            var transports = new FakeQueueTransport(CallType.Success);
            var messageReceiver = QueueTransportPump.CreateDefaultPump(transports);
            int handshakeCallCount = 0;

            var transportContext = new QueueTransportContext
            {
                QueueBindingElement = new FakeBindingElement(),
                ServiceDispatcher = new FakeServiceDispatcher(),
                QueueMessageDispatcher = _ =>
                {
                    handshakeCallCount++;
                    return Task.CompletedTask;
                },
            };
            await messageReceiver.StartPumpAsync(transportContext, default);
            await Task.Delay(100);
            await messageReceiver.StopPumpAsync(default);

            Assert.True(transports.CallCount > 1);
            Assert.True(handshakeCallCount > 1);
        }
    }
}
