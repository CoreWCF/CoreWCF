// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels.Configuration
{
    public class ClassicQueueConfiguration : QueueDeclareConfiguration
    {
        public override string QueueType => RabbitMqQueueType.Classic;

        public ClassicQueueConfiguration AsTemporaryQueue()
        {
            AutoDelete = true;
            return this;
        }
    }
}
