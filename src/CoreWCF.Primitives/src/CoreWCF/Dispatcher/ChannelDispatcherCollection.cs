// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Dispatcher
{
    public class ChannelDispatcherCollection : SynchronizedCollection<ChannelDispatcherBase>
    {
        private readonly ServiceHostBase service;

        internal ChannelDispatcherCollection(ServiceHostBase service, object syncRoot)
            : base(syncRoot)
        {
            this.service = service ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(service));
        }

        protected override void ClearItems()
        {
            ChannelDispatcherBase[] array = new ChannelDispatcherBase[Count];
            CopyTo(array, 0);
            base.ClearItems();

            if (service != null)
            {
                foreach (ChannelDispatcherBase channelDispatcher in array)
                {
                    service.OnRemoveChannelDispatcher(channelDispatcher);
                }
            }
        }

        protected override void InsertItem(int index, ChannelDispatcherBase item)
        {
            if (service != null)
            {
                if (service.State == CommunicationState.Closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(service.GetType().ToString()));
                }

                service.OnAddChannelDispatcher(item);
            }

            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            ChannelDispatcherBase channelDispatcher = Items[index];
            base.RemoveItem(index);
            if (service != null)
            {
                service.OnRemoveChannelDispatcher(channelDispatcher);
            }
        }

        protected override void SetItem(int index, ChannelDispatcherBase item)
        {
            if (service != null)
            {
                if (service.State == CommunicationState.Closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(service.GetType().ToString()));
                }
            }

            if (service != null)
            {
                service.OnAddChannelDispatcher(item);
            }

            ChannelDispatcherBase old;

            lock (SyncRoot)
            {
                old = Items[index];
                base.SetItem(index, item);
            }

            if (service != null)
            {
                service.OnRemoveChannelDispatcher(old);
            }
        }
    }
}