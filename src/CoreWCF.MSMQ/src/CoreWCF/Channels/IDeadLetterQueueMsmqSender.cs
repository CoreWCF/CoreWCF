// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using CoreWCF.Queue;

namespace CoreWCF.Channels
{
    public interface IDeadLetterQueueMsmqSender : IDeadLetterQueueSender
    {
        Task SendToSystem(PipeReader message, Uri endpoint);
    }
}
