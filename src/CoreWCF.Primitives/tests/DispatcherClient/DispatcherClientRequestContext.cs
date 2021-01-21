// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Channels;
using Helpers;

namespace DispatcherClient
{
    public class DispatcherClientRequestContext : RequestContext
    {
        private TaskCompletionSource<Message> _replyMessage;
        private MessageBuffer _bufferedCopy;

        public DispatcherClientRequestContext(Message requestMessage)
        {
            RequestMessage = requestMessage;
            _replyMessage = new TaskCompletionSource<Message>();
        }

        public DispatcherClientRequestContext(System.ServiceModel.Channels.Message requestMessage) : this(TestHelper.ConvertMessage(requestMessage)) { }

        public override Message RequestMessage { get; }

        public Task<Message> ReplyMessageTask
        {
            get
            {
                if (_bufferedCopy != null)
                {
                    return Task.FromResult(_bufferedCopy.CreateMessage());
                }
                else
                {
                    return _replyMessage.Task;
                }
            }
        }

        public override void Abort()
        {
            _replyMessage.TrySetException(new CommunicationException("Aborted"));
        }

        public override Task CloseAsync()
        {
            return Task.CompletedTask;
        }

        public override Task CloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public override Task ReplyAsync(Message message)
        {
            return ReplyAsync(message, CancellationToken.None);
        }

        public override Task ReplyAsync(Message message, CancellationToken token)
        {
            _bufferedCopy = message.CreateBufferedCopy(int.MaxValue);
            _replyMessage.TrySetResult(_bufferedCopy.CreateMessage());
            return Task.CompletedTask;
        }
    }
}
