// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    // TODO: Fold ChannelDispatcherBase implementation into ChannelDispatcher. I believe this was only here for legacy reasons.
    public abstract class ChannelDispatcherBase : CommunicationObject
    {
        public abstract ServiceHostBase Host { get; }

        internal void AttachInternal(ServiceHostBase host)
        {
            Attach(host);
        }

        protected virtual void Attach(ServiceHostBase host)
        {
        }

        internal void DetachInternal(ServiceHostBase host)
        {
            Detach(host);
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