// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public interface ICommunicationObject
    {
        CommunicationState State { get; }
        event System.EventHandler Closed;
        event System.EventHandler Closing;
        event System.EventHandler Faulted;
        event System.EventHandler Opened;
        event System.EventHandler Opening;
        void Abort();
        System.Threading.Tasks.Task CloseAsync();
        System.Threading.Tasks.Task CloseAsync(System.Threading.CancellationToken token);
        System.Threading.Tasks.Task OpenAsync();
        System.Threading.Tasks.Task OpenAsync(System.Threading.CancellationToken token);
    }
}