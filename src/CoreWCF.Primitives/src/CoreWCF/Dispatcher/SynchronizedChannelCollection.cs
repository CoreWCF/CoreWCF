// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Collections.Generic;

namespace CoreWCF.Dispatcher
{
    internal class SynchronizedChannelCollection<TChannel> : SynchronizedCollection<TChannel>
        where TChannel : IChannel
    {
        private readonly EventHandler onChannelClosed;
        private readonly EventHandler onChannelFaulted;

        internal SynchronizedChannelCollection(object syncRoot)
            : base(syncRoot)
        {
            onChannelClosed = new EventHandler(OnChannelClosed);
            onChannelFaulted = new EventHandler(OnChannelFaulted);
        }

        private void AddingChannel(TChannel channel)
        {
            channel.Faulted += onChannelFaulted;
            channel.Closed += onChannelClosed;
        }

        private void RemovingChannel(TChannel channel)
        {
            channel.Faulted -= onChannelFaulted;
            channel.Closed -= onChannelClosed;
        }

        private void OnChannelClosed(object sender, EventArgs args)
        {
            TChannel channel = (TChannel)sender;
            Remove(channel);
        }

        private void OnChannelFaulted(object sender, EventArgs args)
        {
            TChannel channel = (TChannel)sender;
            Remove(channel);
        }

        protected override void ClearItems()
        {
            List<TChannel> items = Items;

            for (int i = 0; i < items.Count; i++)
            {
                RemovingChannel(items[i]);
            }

            base.ClearItems();
        }

        protected override void InsertItem(int index, TChannel item)
        {
            AddingChannel(item);
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            TChannel oldItem = Items[index];

            base.RemoveItem(index);
            RemovingChannel(oldItem);
        }

        protected override void SetItem(int index, TChannel item)
        {
            TChannel oldItem = Items[index];

            AddingChannel(item);
            base.SetItem(index, item);
            RemovingChannel(oldItem);
        }
    }

}