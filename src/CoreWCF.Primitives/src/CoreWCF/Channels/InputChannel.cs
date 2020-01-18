using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    class InputChannel : InputQueueChannel<Message>, IInputChannel
    {
        EndpointAddress localAddress;

        public InputChannel(ChannelManagerBase channelManager, EndpointAddress localAddress)
            : base(channelManager)
        {
            this.localAddress = localAddress;
        }

        public EndpointAddress LocalAddress
        {
            get { return localAddress; }
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IInputChannel))
            {
                return (T)(object)this;
            }

            T baseProperty = base.GetProperty<T>();
            if (baseProperty != null)
            {
                return baseProperty;
            }

            return default(T);
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public virtual Task<Message> ReceiveAsync()
        {
            TimeoutHelper helper = new TimeoutHelper(DefaultReceiveTimeout);
            return ReceiveAsync(helper.GetCancellationToken());
        }

        public virtual Task<Message> ReceiveAsync(CancellationToken token)
        {
            ThrowPending();
            return InputChannel.HelpReceiveAsync(this, token);
        }

        public virtual Task<(Message message, bool success)> TryReceiveAsync(CancellationToken token)
        {
            ThrowPending();
            return base.DequeueAsync(token);
        }

        public Task<bool> WaitForMessageAsync(CancellationToken token)
        {
            ThrowPending();
            return base.WaitForItemAsync(token);
        }

        #region static Helpers to convert TryReceive to Receive
        internal static async Task<Message> HelpReceiveAsync(IInputChannel channel, CancellationToken token)
        {
            var result = await channel.TryReceiveAsync(token);
            if (result.success)
            {
                return result.message;
            }
            else
            {
                // TODO: Derive CancellationToken to carry timeout
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateReceiveTimedOutException(channel, TimeSpan.Zero));
            }
        }

        static Exception CreateReceiveTimedOutException(IInputChannel channel, TimeSpan timeout)
        {
            if (channel.LocalAddress != null)
            {
                return new TimeoutException(SR.Format(SR.ReceiveTimedOut, channel.LocalAddress.Uri.AbsoluteUri, timeout));
            }
            else
            {
                return new TimeoutException(SR.Format(SR.ReceiveTimedOutNoLocalAddress, timeout));
            }
        }
        #endregion
    }

}