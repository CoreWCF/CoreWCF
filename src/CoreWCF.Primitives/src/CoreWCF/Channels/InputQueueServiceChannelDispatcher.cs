// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class InputQueueServiceChannelDispatcher<TItemType> : ServiceChannelBase where TItemType : class, IDisposable
    {
        private readonly ConcurrentQueue<(TItemType item, Action callback)> _itemQueue = new ConcurrentQueue<(TItemType item, Action callback)>();
        private int _dispatching;

        protected InputQueueServiceChannelDispatcher(IDefaultCommunicationTimeouts timeouts) : base(timeouts)
        {
        }

        public int InternalPendingItems => _itemQueue.Count;

        // DispatchAsync is called by a ReliableServiceDispatcher as part of the regular dispatch layer mechanism
        // Once the message has been processed by the reliable layer, InnerDispatchAsync is called to dispatch
        // to the actual service itself.
        public abstract Task DispatchAsync(TItemType item);

        public abstract Task InnerDispatchAsync(TItemType item);

        private async Task DispatchAsync()
        {
            Fx.Assert(_dispatching == 1, "Method should only be called when _dispatching == 1");
            while (_itemQueue.TryDequeue(out var entry))
            {
                await InnerDispatchAsync(entry.item);
                if (entry.callback != null)
                {
                    entry.callback();
                }
            }

            _dispatching = 0;
            Interlocked.MemoryBarrier();
            // It's possible another thread added an item to the queue after TryDequeue returned false
            // and before we set _dispatching to 0. That other thread wouldn't start dispatching when
            // calling EnsureDispatching as _dispatching wasn't yet set to 0. The simple fix for this
            // race condition is to call EnsureDispatching again which will only start dispatching if
            // there are items in the queue.
            EnsureDispatching();
        }

        internal void Enqueue(Exception exception, Action dequeueCallback)
        {
            // TODO: Should we do anything with the exception? Maybe log it? There isn't a 1:1 mapping
            // of an incoming request to a dispatcher call so it's not clear what we could do with it as
            // there isn't a specific request to return it to.
            if (dequeueCallback != null) dequeueCallback();
        }

        internal void Enqueue(TItemType item, Action dequeueCallback)
        {
            _itemQueue.Enqueue((item, dequeueCallback));
            EnsureDispatching();
        }

        internal void EnsureDispatching()
        {
            // If we weren't already dispatching, start dispatching
            if (_itemQueue.Count > 0 && Interlocked.CompareExchange(ref _dispatching, 1, 0) == 0)
            {
                Task.Factory.StartNew(s => ((InputQueueServiceChannelDispatcher<TItemType>)s).DispatchAsync(), this, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
            }
        }
    }
}
