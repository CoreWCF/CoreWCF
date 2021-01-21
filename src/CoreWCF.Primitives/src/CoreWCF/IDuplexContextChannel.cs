// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF
{
    internal interface IDuplexContextChannel : IContextChannel
    {
        bool AutomaticInputSessionShutdown { get; set; }
        InstanceContext CallbackInstance { get; set; }
        Task CloseOutputSessionAsync(CancellationToken token);
    }
}