// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    interface IMessageSource
    {
        Task<Message> ReceiveAsync(CancellationToken token);
        Task<bool> WaitForMessageAsync(CancellationToken token);
    }
}
