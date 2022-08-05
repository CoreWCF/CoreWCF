// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace CoreWCF.Queue
{
    public class QueueOptions
    {
        public List<QueueSettings> Queues { get; } = new List<QueueSettings>();
    }
}
