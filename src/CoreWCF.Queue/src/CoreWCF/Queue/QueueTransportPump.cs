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
        IQueueTransport _transport;
        CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        List<Task> _tasks = new List<Task>();

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
                    queueMessageContext = await _transport.ReceiveQueueMessageContextAsync(cts.Token);

                }
                catch(OperationCanceledException)
                {
                    cts = new(); //log event
                    continue;
                }
                cts.CancelAfter(-1);

                queueMessageContext.QueueTransportContext = queueTransportContext;
                await queueTransportContext.QueueHandShakeDelegate(queueMessageContext);

            }
        }
    }

    public class SqsTransportBindingElement
    {
        // other boiler plate methods


/*
        public QueueTransportPump BuildQueueTransportPump(BindingContext context)
        {
            IQueueTransport queueTransport = CreateMyQueueTransport(context); // The concrete SQS implementation
            return QueueTransportPump.CreateDefaultPump(queueTransport);
        }*/
    }
}

