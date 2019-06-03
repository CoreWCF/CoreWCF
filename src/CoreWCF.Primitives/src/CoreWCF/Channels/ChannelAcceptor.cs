using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    public abstract class ChannelAcceptor<TChannel> : CommunicationObject, IChannelAcceptor<TChannel>
        where TChannel : class, IChannel
    {
        ChannelManagerBase channelManager;

        protected ChannelAcceptor(ChannelManagerBase channelManager)
        {
            this.channelManager = channelManager;
        }

        protected ChannelManagerBase ChannelManager
        {
            get { return channelManager; }
        }

        protected override TimeSpan DefaultCloseTimeout
        {
            get { return channelManager.InternalCloseTimeout; }
        }

        protected override TimeSpan DefaultOpenTimeout
        {
            get { return channelManager.InternalOpenTimeout; }
        }

        public abstract Task<TChannel> AcceptChannelAsync(CancellationToken token);

        public abstract Task<bool> WaitForChannelAsync(CancellationToken token);

        protected override void OnAbort()
        {
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }

}