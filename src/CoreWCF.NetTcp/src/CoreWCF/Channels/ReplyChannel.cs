using CoreWCF.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    class ReplyChannel : InputQueueChannel<RequestContext>, IReplyChannel
    {
        EndpointAddress localAddress;

        public ReplyChannel(ChannelManagerBase channelManager, EndpointAddress localAddress)
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
            if (typeof(T) == typeof(IReplyChannel))
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
            return TaskHelpers.CompletedOrCanceled(token);
        }

        #region static Helpers to convert TryReceiveRequest to ReceiveRequest
        internal static async Task<RequestContext> HelpReceiveRequestAsync(IReplyChannel channel, CancellationToken token)
        {
            var result = await channel.TryReceiveRequestAsync(token);
            if (result.Success)
            {
                return result.Result;
            }
            else
            {
                // TODO: Fix timeout value
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    ReplyChannel.CreateReceiveRequestTimedOutException(channel, TimeSpan.Zero));
            }
        }

        static Exception CreateReceiveRequestTimedOutException(IReplyChannel channel, TimeSpan timeout)
        {
            if (channel.LocalAddress != null)
            {
                return new TimeoutException(SR.Format(SR.ReceiveRequestTimedOut, channel.LocalAddress.Uri.AbsoluteUri, timeout));
            }
            else
            {
                return new TimeoutException(SR.Format(SR.ReceiveRequestTimedOutNoLocalAddress, timeout));
            }
        }
        #endregion


        public Task<RequestContext> ReceiveRequestAsync()
        {
            return ReceiveRequestAsync(new TimeoutHelper(DefaultReceiveTimeout).GetCancellationToken());
        }

        public Task<RequestContext> ReceiveRequestAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled<RequestContext>(token);
            }

            ThrowPending();
            return ReplyChannel.HelpReceiveRequestAsync(this, token);
        }

        public Task<TryAsyncResult<RequestContext>> TryReceiveRequestAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled<TryAsyncResult<RequestContext>>(token);
            }

            ThrowPending();
            return base.DequeueAsync(token);
        }

        public Task<bool> WaitForRequestAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(token);
            }

            ThrowPending();
            return base.WaitForItemAsync(token);
        }
    }

}
