using CoreWCF;
using CoreWCF.Channels;
using Helpers;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DispatcherClient
{
    public class DispatcherClientRequestContext : RequestContext
    {
        private MessageBuffer _bufferedCopy;
        private TaskCompletionSource<Message> _replyMessage;

        public DispatcherClientRequestContext(Message requestMessage)
        {
            RequestMessage = requestMessage;
            _replyMessage = new TaskCompletionSource<Message>();
        }


        public DispatcherClientRequestContext(System.ServiceModel.Channels.Message requestMessage)
        {
            RequestMessage = TestHelper.ConvertMessage(requestMessage);
            _replyMessage = new TaskCompletionSource<Message>();
        }

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

            var registration = token.Register(() => _replyTcs.TrySetCanceled(token));
            _replyTcs.Task.ContinueWith((antecedent) => registration.Dispose());
            return _replyTcs.Task;
        }

        private void SetReplyMessage(Message reply)
        {
            _bufferedCopy = reply.CreateBufferedCopy(int.MaxValue);
            _replyMessage.TrySetResult(_bufferedCopy.CreateMessage());
        }

        public override void Abort()
        {
            _replyMessage.TrySetException(new CommunicationException("Request aborted"));
            return;
        }

        public override Task CloseAsync()
        {
            _replyMessage.TrySetException(new CommunicationException("Request aborted"));
            return Task.CompletedTask;
        }

        public override Task CloseAsync(CancellationToken token)
        {
            _replyMessage.TrySetException(new CommunicationException("Request aborted"));
            return Task.CompletedTask;
        }

        public override Task ReplyAsync(Message message)
        {
            return ReplyAsync(message, CancellationToken.None);
        }

        public override Task ReplyAsync(Message message, CancellationToken token)
        {
            SetReplyMessage(message);
            return Task.CompletedTask;
        }
    }
}
