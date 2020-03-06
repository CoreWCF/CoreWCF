using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    // tracks StreamUpgradeProvider so that the channel can outlive the Listener
    class ConnectionOrientedTransportReplyChannel : ReplyChannel
    {
        StreamUpgradeProvider _upgrade;

        public ConnectionOrientedTransportReplyChannel(ITransportFactorySettings settings, EndpointAddress localAddress)
            : base(settings, localAddress)
        {
        }

        public bool TransferUpgrade(StreamUpgradeProvider upgrade)
        {
            lock (ThisLock)
            {
                if (State != CommunicationState.Opened)
                {
                    return false;
                }

                _upgrade = upgrade;
                return true;
            }
        }

        protected override void OnAbort()
        {
            if (_upgrade != null)
            {
                _upgrade.Abort();
            }
            base.OnAbort();
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            if (_upgrade != null)
            {
                await _upgrade.CloseAsync(token);
            }

            await base.OnCloseAsync(token);
        }
    }
}
