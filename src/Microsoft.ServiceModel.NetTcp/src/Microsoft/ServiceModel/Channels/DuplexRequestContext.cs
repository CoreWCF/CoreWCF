using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    class DuplexRequestContext : RequestContextBase
    {
        IDuplexChannel channel;

        internal DuplexRequestContext(IDuplexChannel channel, Message request, IDefaultCommunicationTimeouts timeouts)
            : base(request, timeouts.CloseTimeout, timeouts.SendTimeout)
        {
            this.channel = channel;
        }

        protected override void OnAbort()
        {
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override Task OnReplyAsync(Message message, CancellationToken token)
        {
            if (message != null)
            {
                return channel.SendAsync(message, token);
            }

            return Task.CompletedTask;
        }
    }
}
