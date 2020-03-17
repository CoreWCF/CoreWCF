using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    class ReplyChannel : ServiceChannelBase, IReplyChannel
    {
        EndpointAddress localAddress;

        public ReplyChannel(IDefaultCommunicationTimeouts timeouts, EndpointAddress localAddress) : base(timeouts)
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

            return default;
        }

        protected override void OnAbort() { }

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