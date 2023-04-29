// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    internal abstract class ServerReliableChannelBinder<TChannel> : ReliableChannelBinder<TChannel>, IServerReliableChannelBinder where TChannel : class, IChannel
    {
    }
}
