// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;

namespace CoreWCF.Queue.Tests.InMemoryQueue;

public class ReceiveContextInterceptor
{
    public CountdownEvent CompleteCountdownEvent { get; } = new(0);

    public CountdownEvent AbandonCountdownEvent { get; } = new(0);
}

public class InMemoryReceiveContext : ReceiveContext
{
    private readonly ReceiveContextInterceptor _receiveContextInterceptor;

    public InMemoryReceiveContext(ReceiveContextInterceptor receiveContextInterceptor)
    {
        _receiveContextInterceptor = receiveContextInterceptor;
    }

    protected override Task OnAbandonAsync(CancellationToken token)
    {
        _receiveContextInterceptor.AbandonCountdownEvent.Signal(1);
        return Task.CompletedTask;
    }

    protected override Task OnCompleteAsync(CancellationToken token)
    {
        _receiveContextInterceptor.CompleteCountdownEvent.Signal(1);
        return Task.CompletedTask;
    }


}
