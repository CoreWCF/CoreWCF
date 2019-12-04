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

        public DispatcherClientRequestContext(Message requestMessage)
        {
            RequestMessage = requestMessage;
        }


        public DispatcherClientRequestContext(System.ServiceModel.Channels.Message requestMessage)
        {
            RequestMessage = TestHelper.ConvertMessage(requestMessage);
        }

        public override Message RequestMessage { get; }

        public Message ReplyMessage
        {
            get
            {
                return _bufferedCopy.CreateMessage();
            }
            private set
            {
                _bufferedCopy = value.CreateBufferedCopy(int.MaxValue);
            }
        }

        public override void Abort()
        {
            throw new NotImplementedException();
        }

        public override Task CloseAsync()
        {
            throw new NotImplementedException();
        }

        public override Task CloseAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public override Task ReplyAsync(Message message)
        {
            return ReplyAsync(message, CancellationToken.None);
        }

        public override Task ReplyAsync(Message message, CancellationToken token)
        {
            ReplyMessage = message;
            return Task.CompletedTask;
        }
    }
}
