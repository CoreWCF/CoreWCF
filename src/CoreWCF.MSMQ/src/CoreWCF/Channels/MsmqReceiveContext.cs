// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Queue.Common;

namespace CoreWCF.Channels
{
    internal class MsmqReceiveContext : ReceiveContext
    {
        private readonly QueueMessageContext _queueMessageContext;
        private readonly DeadLetterQueueSender _deadLetterQueueSender;

        public MsmqReceiveContext(QueueMessageContext queueMessageContext, DeadLetterQueueSender deadLetterQueueSender)
        {
            _queueMessageContext = queueMessageContext;
            _deadLetterQueueSender = deadLetterQueueSender;
        }

        protected override async Task OnAbandonAsync(CancellationToken token)
        {
            if (_queueMessageContext.QueueTransportContext.ServiceDispatcher.Binding is NetMsmqBinding binding &&
                binding.DeadLetterQueue == DeadLetterQueue.Custom)
            {
                await _deadLetterQueueSender.Send(_queueMessageContext.QueueMessageReader, binding.CustomDeadLetterQueue);
            }
            else
            {
                await _deadLetterQueueSender.SendToSystem(_queueMessageContext.QueueMessageReader, _queueMessageContext.LocalAddress.Uri);
            }
        }

        protected override async Task OnCompleteAsync(CancellationToken token)
        {
            // Do nothing
        }
    }
}
