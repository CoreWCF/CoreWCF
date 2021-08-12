// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public interface IRequestChannel : IChannel, ICommunicationObject
    {
        EndpointAddress RemoteAddress { get; }
        System.Uri Via { get; }
        System.Threading.Tasks.Task<Message> RequestAsync(Message message);
        System.Threading.Tasks.Task<Message> RequestAsync(Message message, System.Threading.CancellationToken token);
    }
}