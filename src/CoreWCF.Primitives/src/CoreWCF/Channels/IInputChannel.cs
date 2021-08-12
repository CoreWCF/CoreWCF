// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    public interface IInputChannel : IChannel, ICommunicationObject
    {
        EndpointAddress LocalAddress { get; }
        Task<Message> ReceiveAsync(CancellationToken token);
        Task<(Message message, bool success)> TryReceiveAsync(CancellationToken token);
    }
}
