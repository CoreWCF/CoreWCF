// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels.Framing;

namespace CoreWCF.Channels
{
    internal interface IConnectionReuseHandler
    {
        Task<bool> ReuseConnectionAsync(FramingConnection connection, CancellationToken cancellationToken);
    }
}
