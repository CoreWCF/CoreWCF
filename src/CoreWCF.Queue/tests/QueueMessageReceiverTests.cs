// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CoreWCF.Queue.Tests
{
    //TODO : Modify unit tests
    public class QueueMessageReceiverTests
    {
        /*
        [Fact]
        public async Task QueueMessageReceiver_WhenStated_Success()
        {
            var transports = new[] { new TestQueueTransport(), new TestQueueTransport() };
            var messageReceiver = new QueueMessageReceiver(NullLogger<QueueMessageReceiver>.Instance, transports);

            await messageReceiver.StartAsync(default);

            Assert.All(transports, x => Assert.True(x.IsStarted));
        }

        [Fact]
        public async Task QueueMessageReceiver_WhenStopped_Success()
        {
            var transports = new[] { new TestQueueTransport(), new TestQueueTransport() };
            var messageReceiver = new QueueMessageReceiver(NullLogger<QueueMessageReceiver>.Instance, transports);

            await messageReceiver.StopAsync(default);

            Assert.All(transports, x => Assert.True(x.IsStopped));
        }
        */
    }
    /*
    internal class TestQueueTransport : IQueueTransport
    {
        public bool IsStarted { get; private set; }
        public bool IsStopped { get; private set; }

        public Task StartAsync()
        {
            IsStarted = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsStopped = true;
            return Task.CompletedTask;
        }
    }*/
}
