// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.Queue
{
    public interface IQueueMessagePipelineBuilder
    {
        IServiceProvider Services { get; set; }

        IDictionary<string, object> Properties { get; }

        IQueueMessagePipelineBuilder Use(Func<QueueMessageDispatch, QueueMessageDispatch> middleware);

        IQueueMessagePipelineBuilder New();

        QueueMessageDispatch Build();
    }
}
