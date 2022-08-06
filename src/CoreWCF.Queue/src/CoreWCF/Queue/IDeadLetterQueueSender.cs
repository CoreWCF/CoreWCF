// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace CoreWCF.Queue
{
    public interface IDeadLetterQueueSender
    {
        Task Send(PipeReader message, Uri endpoint);
    }
}
