using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Queue.Common;
using CoreWCF.Runtime;

namespace CoreWCF.Queue.CoreWCF.Queue
{
    public abstract class QueueTransportPump
    {
        public abstract Task StartPumpAsync(QueueTransportContext queueTransportContext, CancellationToken token);
        public abstract Task StopPumpAsync(CancellationToken token);
        public static QueueTransportPump CreateDefaultPump(IQueueTransport queueTransport)
        {
            return new DefaultQueueTransportPump(queueTransport);
        }
    }

    /// <summary>
    /// DefaultQueueTransportPump model serves the purpose of pull model
    /// </summary>
    internal class DefaultQueueTransportPump : QueueTransportPump
    {
        private readonly IQueueTransport _transport;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly List<Task> _tasks = new List<Task>();

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
            if (_transport is IDisposable disposable) disposable.Dispose();
            return Task.WhenAll(_tasks); ;

        }
        private async Task FetchAndProcessAsync(QueueTransportContext queueTransportContext, CancellationToken cancellationToken)
        {
            CancellationTokenSource cts = new ();
            TimeSpan receiveTimeout = queueTransportContext.ServiceDispatcher.Binding.ReceiveTimeout;
            while (true || !cancellationToken.IsCancellationRequested)
            {
                cts.CancelAfter(receiveTimeout);
                QueueMessageContext queueMessageContext = null;
                try
                {
                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken))
                    {
                        queueMessageContext = await _transport.ReceiveQueueMessageContextAsync(linkedCts.Token);
                    }
                }
                catch(OperationCanceledException)
                {
                    cts = new(); 
                    continue;
                }
                if(queueMessageContext == null)
                {
                    cts = new();
                    continue;
                }
                cts.CancelAfter(-1);

                queueMessageContext.QueueTransportContext = queueTransportContext;
                await queueTransportContext.QueueHandShakeDelegate(queueMessageContext);

            }
        }
    }
}

