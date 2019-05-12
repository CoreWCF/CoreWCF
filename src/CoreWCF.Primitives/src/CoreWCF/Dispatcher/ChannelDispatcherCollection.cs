using System;
using System.Collections.Generic;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Dispatcher
{
    public class ChannelDispatcherCollection : SynchronizedCollection<ChannelDispatcherBase>
    {
        ServiceHostBase service;

        internal ChannelDispatcherCollection(ServiceHostBase service, object syncRoot)
            : base(syncRoot)
        {
            this.service = service ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(service));
        }

        protected override void ClearItems()
        {
            throw new PlatformNotSupportedException();
        }

        protected override void InsertItem(int index, ChannelDispatcherBase item)
        {
            if (service != null)
            {
                if (service.State == CommunicationState.Closed)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(service.GetType().ToString()));
            }

            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            ChannelDispatcherBase channelDispatcher = Items[index];
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, ChannelDispatcherBase item)
        {
            if (service != null)
            {
                if (service.State == CommunicationState.Closed)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(service.GetType().ToString()));
            }

            ChannelDispatcherBase old;

            lock (SyncRoot)
            {
                old = Items[index];
                base.SetItem(index, item);
            }
        }
    }
}