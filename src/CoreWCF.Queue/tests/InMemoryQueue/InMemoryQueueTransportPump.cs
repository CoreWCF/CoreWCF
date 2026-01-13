// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Queue.Common;

namespace CoreWCF.Queue.Tests.InMemoryQueue;

public class InMemoryQueueTransportPump : QueueTransportPump, IDisposable
{
    private readonly ConcurrentQueue<string> _queue;
    private readonly ReceiveContextInterceptor _receiveContextInterceptor;
    private CancellationTokenSource _cts;
    private ManualResetEventSlim _mres;
    public InMemoryQueueTransportPump(ConcurrentQueue<string> queue, ReceiveContextInterceptor receiveContextInterceptor)
    {
        _queue = queue;
        _receiveContextInterceptor = receiveContextInterceptor;
    }

    public override Task StartPumpAsync(QueueTransportContext queueTransportContext, CancellationToken token)
    {
        _cts = new CancellationTokenSource();
        _mres = new ManualResetEventSlim();
        CancellationToken ct = CancellationTokenSource.CreateLinkedTokenSource(token, _cts.Token).Token;

        Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                if (_queue.TryDequeue(out string message))
                {
                    await OnConsume(message, queueTransportContext);
                }
                else
                {
                    // Add a small delay when queue is empty to avoid tight loop
                    await Task.Delay(10, ct);
                }
            }

            _mres.Set();
        });

        return Task.CompletedTask;
    }

    private async Task OnConsume(string message, QueueTransportContext queueTransportContext)
    {
        var reader = PipeReader.Create(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(message)));
        await queueTransportContext.QueueMessageDispatcher(GetContext(reader, queueTransportContext));
    }

    private QueueMessageContext GetContext(PipeReader reader, QueueTransportContext transportContext)
    {
        var receiveContext = new InMemoryReceiveContext(_receiveContextInterceptor);
        var context = new QueueMessageContext
        {
            QueueMessageReader = reader,
            LocalAddress = new EndpointAddress(transportContext.ServiceDispatcher.BaseAddress),
            QueueTransportContext = transportContext,
            ReceiveContext = receiveContext
        };

        return context;
    }

    public override Task StopPumpAsync(CancellationToken token)
    {
        _cts.Cancel();
        _cts.Dispose();
        _mres.Wait(token);
        _mres.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _mres?.Dispose();
    }
}
