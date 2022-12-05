// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CoreWCF.Queue.Common.Configuration
{
    public delegate Task QueueMessageDispatcherDelegate(QueueMessageContext context);

    public interface IQueueMiddlewareBuilder
    {
        IServiceProvider Services { get; set; }

        IDictionary<string, object> Properties { get; }

        IQueueMiddlewareBuilder Use(Func<QueueMessageDispatcherDelegate, QueueMessageDispatcherDelegate> middleware);

        IQueueMiddlewareBuilder New();

        QueueMessageDispatcherDelegate Build();
    }
}
