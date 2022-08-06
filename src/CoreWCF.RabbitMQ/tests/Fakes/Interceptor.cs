// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Queue;

namespace CoreWCF.RabbitMQ.Tests.Fakes
{
    public class Interceptor
    {
        public string Name { get; private set; }

        public void SetName(string name)
        {
            Name = name;
        }
    }

    internal class TestConnectionHandler : IQueueConnectionHandler
    {
        public int CallCount { get; private set; }

        public QueueMessageContext GetContext(PipeReader reader, string queueUrl)
        {
            CallCount++;
            return new QueueMessageContext
            {
                QueueTransportContext = new QueueTransportContext
                {
                    QueueHandShakeDelegate = _ => Task.CompletedTask,
                },
                Properties = new Dictionary<string, object>(),
            };
        }
    }
}
