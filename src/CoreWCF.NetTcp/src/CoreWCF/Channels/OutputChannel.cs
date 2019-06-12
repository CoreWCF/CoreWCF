using System;
using System.Diagnostics;
using CoreWCF.Runtime.Diagnostics;
using CoreWCF;
using CoreWCF.Diagnostics;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using System.Threading;

namespace CoreWCF.Channels
{
    abstract class OutputChannel : ChannelBase, IOutputChannel
    {
        protected OutputChannel(ChannelManagerBase manager)
            : base(manager)
        {
        }

        public abstract EndpointAddress RemoteAddress { get; }
        public abstract Uri Via { get; }

        public Task SendAsync(Message message)
        {
            var helper = new TimeoutHelper(DefaultSendTimeout);
            return SendAsync(message, helper.GetCancellationToken());
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            if (message == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));

            if(token.IsCancellationRequested)
            {
                return Task.FromCanceled(token);
            }

            ThrowIfDisposedOrNotOpen();

            AddHeadersTo(message);
            return OnSendAsync(message, token);
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IOutputChannel))
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

        protected abstract Task OnSendAsync(Message message, CancellationToken token);

        protected virtual void AddHeadersTo(Message message)
        {
        }
    }

}
