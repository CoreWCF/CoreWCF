// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipelines;
using CoreWCF.Queue;

namespace CoreWCF.Configuration
{
    public interface IQueueConnectionHandler
    {
        QueueMessageContext GetContext(PipeReader reader, string queueUrl);
    }
}
