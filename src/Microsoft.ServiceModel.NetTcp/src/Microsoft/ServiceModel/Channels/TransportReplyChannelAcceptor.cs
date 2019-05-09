using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    class TransportReplyChannelAcceptor : ReplyChannelAcceptor
    {
        TransportManagerContainer transportManagerContainer;
        TransportChannelListener listener;

        public TransportReplyChannelAcceptor(TransportChannelListener listener)
            : base(listener)
        {
            this.listener = listener;
        }

        protected override ReplyChannel OnCreateChannel()
        {
            return new TransportReplyChannel(ChannelManager, null);
        }

        protected override void OnOpening()
        {
            base.OnOpening();
            transportManagerContainer = listener.GetTransportManagers();
            listener = null;
        }

        protected override void OnAbort()
        {
            base.OnAbort();
            if (transportManagerContainer != null && !TransferTransportManagers())
            {
                transportManagerContainer.Abort();
            }
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            await base.OnCloseAsync(token);
            if (transportManagerContainer != null && !TransferTransportManagers())
            {
                await transportManagerContainer.CloseAsync(token);
            }
        }

        // used to decouple our channel and listener lifetimes
        bool TransferTransportManagers()
        {
            TransportReplyChannel singletonChannel = (TransportReplyChannel)base.GetCurrentChannel();
            if (singletonChannel == null)
            {
                return false;
            }
            else
            {
                return singletonChannel.TransferTransportManagers(transportManagerContainer);
            }
        }

        // tracks TransportManager so that the channel can outlive the Listener
        protected class TransportReplyChannel : ReplyChannel
        {
            TransportManagerContainer transportManagerContainer;

            public TransportReplyChannel(ChannelManagerBase channelManager, EndpointAddress localAddress)
                : base(channelManager, localAddress)
            {
            }

            public bool TransferTransportManagers(TransportManagerContainer transportManagerContainer)
            {
                lock (ThisLock)
                {
                    if (State != CommunicationState.Opened)
                    {
                        return false;
                    }

                    this.transportManagerContainer = transportManagerContainer;
                    return true;
                }
            }

            protected override void OnAbort()
            {
                if (transportManagerContainer != null)
                {
                    transportManagerContainer.Abort();
                }
                base.OnAbort();
            }

            protected override async Task OnCloseAsync(CancellationToken token)
            {
                if (transportManagerContainer != null)
                {
                    await transportManagerContainer.CloseAsync(token);
                }
                await base.OnCloseAsync(token);
            }
        }
    }
}
