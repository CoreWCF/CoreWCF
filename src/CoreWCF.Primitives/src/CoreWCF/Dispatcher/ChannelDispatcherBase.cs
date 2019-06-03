using System;
using System.Threading;
using CoreWCF.Channels;
using System.Threading.Tasks;

namespace CoreWCF.Dispatcher
{
    public abstract class ChannelDispatcherBase : CommunicationObject
    {
        public abstract ServiceHostBase Host { get; }

        protected virtual void Attach(ServiceHostBase host)
        {
        }

        protected virtual void Detach(ServiceHostBase host)
        {
        }

        public virtual Task CloseInputAsync()
        {
            return Task.CompletedTask;
        }

        internal virtual Task CloseInputAsync(CancellationToken token)
        {
            return CloseInputAsync(); // back-compat
        }
    }
}