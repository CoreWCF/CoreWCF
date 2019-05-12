using System;
using System.Collections.Generic;
using CoreWCF.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    class SynchronizedChannelCollection<TChannel> : SynchronizedCollection<TChannel>
        where TChannel : IChannel
    {
        EventHandler onChannelClosed;
        EventHandler onChannelFaulted;

        internal SynchronizedChannelCollection(object syncRoot)
            : base(syncRoot)
        {
            onChannelClosed = new EventHandler(OnChannelClosed);
            onChannelFaulted = new EventHandler(OnChannelFaulted);
        }

        void AddingChannel(TChannel channel)
        {
            channel.Faulted += onChannelFaulted;
            channel.Closed += onChannelClosed;
        }

        void RemovingChannel(TChannel channel)
        {
            channel.Faulted -= onChannelFaulted;
            channel.Closed -= onChannelClosed;
        }

        void OnChannelClosed(object sender, EventArgs args)
        {
            TChannel channel = (TChannel)sender;
            Remove(channel);
        }

        void OnChannelFaulted(object sender, EventArgs args)
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