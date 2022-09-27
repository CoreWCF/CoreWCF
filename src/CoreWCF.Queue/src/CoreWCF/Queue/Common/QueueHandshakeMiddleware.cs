// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Queue.Common.Configuration;

namespace CoreWCF.Queue.Common
{
    public class QueueHandShakeMiddleWare
    {
        private readonly IQueueMiddlewareBuilder _queueMiddlewareBuilder;

        public QueueHandShakeMiddleWare(IQueueMiddlewareBuilder queueBuilder)
        {
            _queueMiddlewareBuilder = InitQueueMiddleWare(queueBuilder);
        }

        private static IQueueMiddlewareBuilder InitQueueMiddleWare(IQueueMiddlewareBuilder queueBuilder)
        {
            queueBuilder.UseMiddleware<QueueFetchMessage>();
            queueBuilder.UseMiddleware<QueueProcessMessage>();
            return queueBuilder;
        }

        public QueueMessageDispatcherDelegate Build()
        {
            return _queueMiddlewareBuilder.Build();
        }
    }
}
