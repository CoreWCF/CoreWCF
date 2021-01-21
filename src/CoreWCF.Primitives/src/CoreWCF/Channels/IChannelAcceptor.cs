// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    interface IChannelAcceptor<TChannel> : ICommunicationObject
        where TChannel : class, IChannel
    {
        Task<TChannel> AcceptChannelAsync(CancellationToken token);
        Task<bool> WaitForChannelAsync(CancellationToken token);
    }
}