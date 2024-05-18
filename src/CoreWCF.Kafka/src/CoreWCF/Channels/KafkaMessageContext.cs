// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Queue.Common;

namespace CoreWCF.Channels;

internal class KafkaMessageContext : QueueMessageContext
{
    protected override void OnRequestMessageSet(Message message)
    {
        if (IsRegexSubscription)
        {
            message.Headers.To = LocalAddress.Uri;
        }
    }

    internal bool IsRegexSubscription { get; set; }
}
