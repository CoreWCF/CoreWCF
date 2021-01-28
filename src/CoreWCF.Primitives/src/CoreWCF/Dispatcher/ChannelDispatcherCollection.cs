// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Dispatcher
{
    public class ChannelDispatcherCollection : SynchronizedCollection<ChannelDispatcherBase>
    {
        private readonly ServiceHostBase _service;

        internal ChannelDispatcherCollection(ServiceHostBase service, object syncRoot)
            : base(syncRoot)
        {
            _service = service ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(service));
        }

        protected override void ClearItems()
        {
            ChannelDispatcherBase[] array = new ChannelDispatcherBase[Count];
            CopyTo(array, 0);
            base.ClearItems();

            if (_service != null)
            {
                foreach (ChannelDispatcherBase channelDispatcher in array)
                {
                    _service.OnRemoveChannelDispatcher(channelDispatcher);
                }
            }
        }

        protected override void InsertItem(int index, ChannelDispatcherBase item)
        {
            if (_service != null)
            {
                if (_service.State == CommunicationState.Closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(_service.GetType().ToString()));
                }

                _service.OnAddChannelDispatcher(item);
            }

            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            ChannelDispatcherBase channelDispatcher = Items[index];
            base.RemoveItem(index);
            if (_service != null)
            {
                _service.OnRemoveChannelDispatcher(channelDispatcher);
            }
        }

        protected override void SetItem(int index, ChannelDispatcherBase item)
        {
            if (_service != null)
            {
                if (_service.State == CommunicationState.Closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(_service.GetType().ToString()));
                }
            }

            if (_service != null)
            {
                _service.OnAddChannelDispatcher(item);
            }

            ChannelDispatcherBase old;

            lock (SyncRoot)
            {
                old = Items[index];
                base.SetItem(index, item);
            }

            if (_service != null)
            {
                _service.OnRemoveChannelDispatcher(old);
            }
        }
    }
}