// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Queue;
using Microsoft.Extensions.Logging;
using MSMQ.Messaging;

namespace CoreWCF.Channels
{
    internal class MsmqNetCoreTransport : IQueueTransport
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly ILogger<MsmqNetCoreTransport> _logger;
        private readonly QueueSettings _queueSettings;
        private readonly List<Task> _tasks = new List<Task>();
        private readonly IQueueConnectionHandler _msmqConnectionHandler;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private bool _isBeginConsume;

        public MsmqNetCoreTransport(
            ILoggerFactory loggerFactory,
            QueueSettings queueSettings,
            IQueueConnectionHandler msmqConnectionHandler,
            IServiceBuilder serviceBuilder)
        {
            _queueSettings = queueSettings;
            _msmqConnectionHandler = msmqConnectionHandler;
            _logger = loggerFactory.CreateLogger<MsmqNetCoreTransport>();
            serviceBuilder.Opened += ServiceIsStarted;
        }

        private void ServiceIsStarted(object sender, EventArgs e)
        {
            // Protection for the situation when ServiceDispatcher is not initialized
            _isBeginConsume = true;
        }

        public Task StartAsync()
        {
            for (int i = 0; i < _queueSettings.ConcurrencyLevel; i++)
            {
                var task = Task.Factory.StartNew(ReceiveMessages, TaskCreationOptions.LongRunning);
                _tasks.Add(task);
            }

            return Task.CompletedTask;
        }

        private async Task ReceiveMessages()
        {
            var nativeQueueName = MsmqQueueNameConverter.GetMsmqFormatQueueName(_queueSettings.QueueName);
            var queue = new MessageQueue(nativeQueueName);
            var enumerator = queue.GetMessageEnumerator2();

            while (!_cts.IsCancellationRequested)
            {
                if (!_isBeginConsume)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    continue;
                }

                if (enumerator.MoveNext())
                {
                    await _semaphore.WaitAsync();
                    _logger.LogInformation("Receiving message from msmq");
                    var message = queue.Receive();
                    var reader = PipeReader.Create(message.BodyStream);

                    var endpointUrl = MsmqQueueNameConverter.GetEndpointUrl(_queueSettings.QueueName);
                    var messageContext = _msmqConnectionHandler.GetContext(reader, endpointUrl);
                    var dispatch = messageContext.QueueTransportContext.QueueHandShakeDelegate;

                    await dispatch(messageContext);
                    _semaphore.Release();
                }

                await Task.Delay(1);
            }
        }

        public Task StopAsync()
        {
            _cts.Cancel();
            Task.WaitAll(_tasks.ToArray());
            return Task.CompletedTask;
        }
    }
}
