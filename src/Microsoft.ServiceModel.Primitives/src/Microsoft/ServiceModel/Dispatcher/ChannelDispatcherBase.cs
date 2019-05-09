using System;
using System.Threading;
using Microsoft.ServiceModel.Channels;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Dispatcher
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