// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.MSMQ.Tests
{
    public class MsmqQueueNameConverterTests
    {
        [Fact]
        public void GetMsmqFormatQueueNameTest()
        {
            var uri = new Uri("net.msmq://localhost/private/QueueName");
            string result = MsmqQueueNameConverter.GetMsmqFormatQueueName(uri);
            Assert.Equal(".\\Private$\\QueueName", result);
        }

        [Fact]
        public void GetEndpointUrlTest()
        {
            string result = MsmqQueueNameConverter.GetEndpointUrl("QueueName");
            Assert.Equal("net.msmq://localhost/private/QueueName", result);
        }
    }
}
