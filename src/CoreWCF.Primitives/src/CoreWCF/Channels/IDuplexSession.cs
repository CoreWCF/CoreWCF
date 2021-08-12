// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public interface IDuplexSession : IInputSession, IOutputSession, ISession
    {
        System.Threading.Tasks.Task CloseOutputSessionAsync();
        System.Threading.Tasks.Task CloseOutputSessionAsync(System.Threading.CancellationToken token);
    }
}