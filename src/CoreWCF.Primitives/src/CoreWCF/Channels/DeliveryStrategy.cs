// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Runtime;
using System.Collections.Generic;
using System;

namespace CoreWCF.Channels
{
    internal abstract class DeliveryStrategy<ItemType> : IDisposable where ItemType : class, IDisposable
    {
        private InputQueueServiceChannelDispatcher<ItemType> _channel;
        private Action _dequeueCallback;
        private readonly int _quota;

        public DeliveryStrategy(InputQueueServiceChannelDispatcher<ItemType> channel, int quota)
        {
            if (quota <= 0)
            {
                throw Fx.AssertAndThrow("Argument quota must be positive.");
            }

            _channel = channel;
            _quota = quota;
        }

        protected InputQueueServiceChannelDispatcher<ItemType> Channel
        {
            get
            {
                return _channel;
            }
        }

        public Action DequeueCallback
        {
            get
            {
                return _dequeueCallback;
            }
            set
            {
                _dequeueCallback = value;
            }
        }

        public abstract int EnqueuedCount
        {
            get;
        }

        protected int Quota
        {
            get
            {
                return _quota;
            }
        }

        public abstract bool CanEnqueue(long sequenceNumber);

        public virtual void Dispose()
        {
        }

        public abstract void Enqueue(ItemType item, long sequenceNumber);
    }

    internal class OrderedDeliveryStrategy<ItemType> : DeliveryStrategy<ItemType> where ItemType : class, IDisposable
    {
        private readonly bool _isEnqueueInOrder;
        private readonly Dictionary<long, ItemType> _items;
        private Action<object> _onDispatchCallback;
        private long _windowStart;

        public OrderedDeliveryStrategy(
            InputQueueServiceChannelDispatcher<ItemType> channel,
            int quota,
            bool isEnqueueInOrder)
            : base(channel, quota)
        {
            _isEnqueueInOrder = isEnqueueInOrder;
            _items = new Dictionary<long, ItemType>();
            _windowStart = 1;
        }

        public override int EnqueuedCount
        {
            get
            {
                return Channel.InternalPendingItems + _items.Count;
            }
        }

        public override bool CanEnqueue(long sequenceNumber)
        {
            if (EnqueuedCount >= Quota)
            {
                return false;
            }

            if (_isEnqueueInOrder && (sequenceNumber > _windowStart))
            {
                return false;
            }

            return Channel.InternalPendingItems + sequenceNumber - _windowStart < Quota;
        }

        public override void Enqueue(ItemType item, long sequenceNumber)
        {
            if (sequenceNumber > _windowStart)
            {
                _items.Add(sequenceNumber, item);
                return;
            }

            _windowStart++;

            while (_items.ContainsKey(_windowStart))
            {
                Channel.Enqueue(item, DequeueCallback);
                item = _items[_windowStart];
                _items.Remove(_windowStart);
                _windowStart++;
            }

            Channel.Enqueue(item, DequeueCallback);
        }

        private static void DisposeItems(Dictionary<long, ItemType>.Enumerator items)
        {
            if (items.MoveNext())
            {
                using (ItemType item = items.Current.Value)
                {
                    DisposeItems(items);
                }
            }
        }

        public override void Dispose()
        {
            DisposeItems(_items.GetEnumerator());
            _items.Clear();

            base.Dispose();
        }
    }

    internal class UnorderedDeliveryStrategy<ItemType> : DeliveryStrategy<ItemType> where ItemType : class, IDisposable
    {
        public UnorderedDeliveryStrategy(InputQueueServiceChannelDispatcher<ItemType> channel, int quota) : base(channel, quota) { }

        public override int EnqueuedCount => Channel.InternalPendingItems;
        public override bool CanEnqueue(long sequenceNumber) => (EnqueuedCount < Quota);

        public override void Enqueue(ItemType item, long sequenceNumber)
        {
            Channel.Enqueue(item, DequeueCallback);
        }
    }
}
