// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Queue.Common
{
    /// <summary>
    /// DefaultQueueTransportPump model serves the purpose of pull model
    /// </summary>
    internal class DefaultQueueTransportPump : QueueTransportPump
    {
        private readonly IQueueTransport _transport;
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly List<Task> _tasks = new();

        public DefaultQueueTransportPump(IQueueTransport queueTransport)
        {
            _transport = queueTransport;
        }

        public override Task StartPumpAsync(QueueTransportContext queueTransportContext, CancellationToken token)
        {
            for (int i = 0; i < queueTransportContext.QueueBindingElement.MaxPendingReceives; i++)
            {
                _tasks.Add(FetchAndProcessAsync(queueTransportContext, _cancellationTokenSource.Token));
            }

            return Task.CompletedTask;
        }

        public override Task StopPumpAsync(CancellationToken token)
        {
            _cancellationTokenSource.Cancel();
            if (_transport is IDisposable disposable)
            {
                disposable.Dispose();
            }

            return Task.WhenAll(_tasks);
        }

        private async Task FetchAndProcessAsync(QueueTransportContext queueTransportContext, CancellationToken token)
        {
            CancellationTokenSource cts = new();
            TimeSpan receiveTimeout = queueTransportContext.ServiceDispatcher.Binding.ReceiveTimeout;
            while (!token.IsCancellationRequested)
            {
                cts.CancelAfter(receiveTimeout);
                QueueMessageContext queueMessageContext;
                try
                {
                    using var linkedCts =
                        CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token);
                    queueMessageContext = await _transport.ReceiveQueueMessageContextAsync(linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    cts = new();
                    continue;
                }

                if (queueMessageContext == null)
                {
                    cts = new();
                    continue;
                }

                cts.CancelAfter(-1);

                queueMessageContext.QueueTransportContext = queueTransportContext;
                await queueTransportContext.QueueMessageDispatcher(queueMessageContext);
            }
        }
    }
}
