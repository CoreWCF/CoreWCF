// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace CoreWCF.Channels
{
    internal class RabbitMqReceiveContext : ReceiveContext
    {
        private readonly ulong _deliveryTag;
        private readonly IModel _sourceChannel;
        private readonly bool _isAutoAck;
        private readonly ILogger _logger;

        public RabbitMqReceiveContext(
            ulong deliveryTag,
            IModel sourceChannel,
            bool isAutoAck,
            ILogger logger)
        {
            _deliveryTag = deliveryTag;
            _sourceChannel = sourceChannel;
            _isAutoAck = isAutoAck;
            _logger = logger;
        }

        protected override async Task OnAbandonAsync(CancellationToken token)
        {
            await Task.Run(() =>
                _logger.LogError($"Dispatch failed for message with delivery tag: {_deliveryTag}"),
                token);

            if (!_isAutoAck)
            {
                // Note: The default behavior is to requeue the message. This works well with Quorum queues
                // as they have a delivery limit, but it is a known issue that Classic queues do not have a
                // delivery limit, exposing them to risk of a DOS.
                // GitHub Issue: https://github.com/rabbitmq/rabbitmq-server/issues/2013
                _sourceChannel.BasicNack(_deliveryTag, false, true);
            }
        }

        protected override async Task OnCompleteAsync(CancellationToken token)
        {
            if (!_isAutoAck)
            {
                await Task.Run(() => _sourceChannel.BasicAck(_deliveryTag, false), token);
            }
        }
    }
}
