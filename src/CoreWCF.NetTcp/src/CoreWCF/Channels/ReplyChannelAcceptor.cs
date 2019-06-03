using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Channels
{
    class ReplyChannelAcceptor : SingletonChannelAcceptor<IReplyChannel, ReplyChannel, RequestContext>
    {
        public ReplyChannelAcceptor(ChannelManagerBase channelManager)
            : base(channelManager)
        {
        }

        protected override ReplyChannel OnCreateChannel()
        {
            return new ReplyChannel(ChannelManager, null);
        }

        protected override void OnTraceMessageReceived(RequestContext requestContext)
        {
        }
    }
}
