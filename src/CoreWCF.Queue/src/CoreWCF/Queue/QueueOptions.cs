// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Queue.Common
{
    public class QueueOptions
    {
        public int ConcurrencyLevel { get; set; }
        public string QueueName { get; set; }
    }
}
