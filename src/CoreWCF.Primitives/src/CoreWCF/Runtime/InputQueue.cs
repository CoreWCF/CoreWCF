// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Runtime
{
    internal sealed class InputQueue<T> : IDisposable where T : class
    {
        private static Action<object> s_completeOutstandingReadersCallback;
        private static Action<object> s_completeWaitersFalseCallback;
        private static Action<object> s_completeWaitersTrueCallback;
        private static Action<object> s_onDispatchCallback;
        private static Action<object> s_onInvokeDequeuedCallback;
        private QueueState _queueState;
        private readonly ItemQueue _itemQueue;
        private readonly Queue<IQueueReader> _readerQueue;
        private readonly List<IQueueWaiter> _waiterList;

        public InputQueue()
        {
            _itemQueue = new ItemQueue();
            _readerQueue = new Queue<IQueueReader>();
            _waiterList = new List<IQueueWaiter>();
            _queueState = QueueState.Open;
        }

        public InputQueue(Func<Action<AsyncCallback, IAsyncResult>> asyncCallbackGenerator)
            : this()
        {
            Fx.Assert(asyncCallbackGenerator != null, "use default ctor if you don't have a generator");
            AsyncCallbackGenerator = asyncCallbackGenerator;
        }

        public int PendingCount
        {
            get
            {
                lock (ThisLock)
                {
                    return _itemQueue.ItemCount;
                }
            }
        }

        // Users like ServiceModel can hook this abort ICommunicationObject or handle other non-IDisposable objects
        public Action<T> DisposeItemCallback
        {
            get;
            set;
        }

        // Users like ServiceModel can hook this to wrap the AsyncQueueReader callback functionality for tracing, etc
        private Func<Action<AsyncCallback, IAsyncResult>> AsyncCallbackGenerator
        {
            get;
            set;
        }

        private object ThisLock
        {
            get { return _itemQueue; }
        }

        public void Close()
        {
            Dispose();
        }

        public async Task<T> DequeueAsync(CancellationToken token)
        {
            var dequeued = await TryDequeueAsync(token);

            if (!dequeued.success)
            {
                // TODO: Create derived CancellationToken which carries original timeout with it
                throw Fx.Exception.AsError(new TimeoutException(SR.Format(SR.TimeoutInputQueueDequeue, null)));
            }

            return dequeued.result;
        }

        public async Task<(T result, bool success)> TryDequeueAsync(CancellationToken token)
        {
            WaitQueueReader reader = null;
            Item item = new Item();

            lock (ThisLock)
            {
                if (_queueState == QueueState.Open)
                {
                    if (_itemQueue.HasAvailableItem)
                    {
                        item = _itemQueue.DequeueAvailableItem();
                    }
                    else
                    {
                        reader = new WaitQueueReader(this);
                        _readerQueue.Enqueue(reader);
                    }
                }
                else if (_queueState == QueueState.Shutdown)
                {
                    if (_itemQueue.HasAvailableItem)
                    {
                        item = _itemQueue.DequeueAvailableItem();
                    }
                    else if (_itemQueue.HasAnyItem)
                    {
                        reader = new WaitQueueReader(this);
                        _readerQueue.Enqueue(reader);
                    }
                    else
                    {
                        return (default(T), true);
                    }
                }
                else // queueState == QueueState.Closed
                {
                    return (default(T), true);
                }
            }

            if (reader != null)
            {
                return await reader.WaitAsync(token);
            }
            else
            {
                InvokeDequeuedCallback(item.DequeuedCallback);
                return (item.GetValue(), true);
            }
        }

        public void Dispatch()
        {
            IQueueReader reader = null;
            Item item = new Item();
            IQueueReader[] outstandingReaders = null;
            IQueueWaiter[] waiters = null;
            bool itemAvailable = true;

            lock (ThisLock)
            {
                itemAvailable = !((_queueState == QueueState.Closed) || (_queueState == QueueState.Shutdown));
                GetWaiters(out waiters);

                if (_queueState != QueueState.Closed)
                {
                    _itemQueue.MakePendingItemAvailable();

                    if (_readerQueue.Count > 0)
                    {
                        item = _itemQueue.DequeueAvailableItem();
                        reader = _readerQueue.Dequeue();

                        if (_queueState == QueueState.Shutdown && _readerQueue.Count > 0 && _itemQueue.ItemCount == 0)
                        {
                            outstandingReaders = new IQueueReader[_readerQueue.Count];
                            _readerQueue.CopyTo(outstandingReaders, 0);
                            _readerQueue.Clear();

                            itemAvailable = false;
                        }
                    }
                }
            }

            if (outstandingReaders != null)
            {
                if (s_completeOutstandingReadersCallback == null)
                {
                    s_completeOutstandingReadersCallback = CompleteOutstandingReadersCallback;
                }

                ActionItem.Schedule(s_completeOutstandingReadersCallback, outstandingReaders);
            }

            if (waiters != null)
            {
                CompleteWaitersLater(itemAvailable, waiters);
            }

            if (reader != null)
            {
                InvokeDequeuedCallback(item.DequeuedCallback);
                reader.Set(item);
            }
        }

        public void EnqueueAndDispatch(T item)
        {
            EnqueueAndDispatch(item, null);
        }

        // dequeuedCallback is called as an item is dequeued from the InputQueue.  The 
        // InputQueue lock is not held during the callback.  However, the user code will
        // not be notified of the item being available until the callback returns.  If you
        // are not sure if the callback will block for a long time, then first call 
        // IOThreadScheduler.ScheduleCallback to get to a "safe" thread.
        public void EnqueueAndDispatch(T item, Action dequeuedCallback)
        {
            EnqueueAndDispatch(item, dequeuedCallback, true);
        }

        public void EnqueueAndDispatch(Exception exception, Action dequeuedCallback, bool canDispatchOnThisThread)
        {
            Fx.Assert(exception != null, "EnqueueAndDispatch: exception parameter should not be null");
            EnqueueAndDispatch(new Item(exception, dequeuedCallback), canDispatchOnThisThread);
        }

        public void EnqueueAndDispatch(T item, Action dequeuedCallback, bool canDispatchOnThisThread)
        {
            Fx.Assert(item != null, "EnqueueAndDispatch: item parameter should not be null");
            EnqueueAndDispatch(new Item(item, dequeuedCallback), canDispatchOnThisThread);
        }

        public bool EnqueueWithoutDispatch(T item, Action dequeuedCallback)
        {
            Fx.Assert(item != null, "EnqueueWithoutDispatch: item parameter should not be null");
            return EnqueueWithoutDispatch(new Item(item, dequeuedCallback));
        }

        public bool EnqueueWithoutDispatch(Exception exception, Action dequeuedCallback)
        {
            Fx.Assert(exception != null, "EnqueueWithoutDispatch: exception parameter should not be null");
            return EnqueueWithoutDispatch(new Item(exception, dequeuedCallback));
        }


        public void Shutdown()
        {
            Shutdown(null);
        }

        // Don't let any more items in. Differs from Close in that we keep around
        // existing items in our itemQueue for possible future calls to Dequeue
        public void Shutdown(Func<Exception> pendingExceptionGenerator)
        {
            IQueueReader[] outstandingReaders = null;
            lock (ThisLock)
            {
                if (_queueState == QueueState.Shutdown)
                {
                    return;
                }

                if (_queueState == QueueState.Closed)
                {
                    return;
                }

                _queueState = QueueState.Shutdown;

                if (_readerQueue.Count > 0 && _itemQueue.ItemCount == 0)
                {
                    outstandingReaders = new IQueueReader[_readerQueue.Count];
                    _readerQueue.CopyTo(outstandingReaders, 0);
                    _readerQueue.Clear();
                }
            }

            if (outstandingReaders != null)
            {
                for (int i = 0; i < outstandingReaders.Length; i++)
                {
                    Exception exception = (pendingExceptionGenerator != null) ? pendingExceptionGenerator() : null;
                    outstandingReaders[i].Set(new Item(exception, null));
                }
            }
        }


        public Task<bool> WaitForItemAsync(CancellationToken token)
        {
            WaitQueueWaiter waiter = null;
            bool itemAvailable = false;

            lock (ThisLock)
            {
                if (_queueState == QueueState.Open)
                {
                    if (_itemQueue.HasAvailableItem)
                    {
                        itemAvailable = true;
                    }
                    else
                    {
                        waiter = new WaitQueueWaiter();
                        _waiterList.Add(waiter);
                    }
                }
                else if (_queueState == QueueState.Shutdown)
                {
                    if (_itemQueue.HasAvailableItem)
                    {
                        itemAvailable = true;
                    }
                    else if (_itemQueue.HasAnyItem)
                    {
                        waiter = new WaitQueueWaiter();
                        _waiterList.Add(waiter);
                    }
                    else
                    {
                        return Task.FromResult(true);
                    }
                }
                else // queueState == QueueState.Closed
                {
                    return Task.FromResult(true);
                }
            }

            if (waiter != null)
            {
                return waiter.WaitAsync(token);
            }
            else
            {
                return Task.FromResult(itemAvailable);
            }
        }

        public void Dispose()
        {
            bool dispose = false;

            lock (ThisLock)
            {
                if (_queueState != QueueState.Closed)
                {
                    _queueState = QueueState.Closed;
                    dispose = true;
                }
            }

            if (dispose)
            {
                while (_readerQueue.Count > 0)
                {
                    IQueueReader reader = _readerQueue.Dequeue();
                    reader.Set(default(Item));
                }

                while (_itemQueue.HasAnyItem)
                {
                    Item item = _itemQueue.DequeueAnyItem();
                    DisposeItem(item);
                    InvokeDequeuedCallback(item.DequeuedCallback);
                }
            }
        }

        private void DisposeItem(Item item)
        {
            T value = item.Value;
            if (value != null)
            {
                if (value is IDisposable)
                {
                    ((IDisposable)value).Dispose();
                }
                else
                {
                    Action<T> disposeItemCallback = DisposeItemCallback;
                    if (disposeItemCallback != null)
                    {
                        disposeItemCallback(value);
                    }
                }
            }
        }

        private static void CompleteOutstandingReadersCallback(object state)
        {
            IQueueReader[] outstandingReaders = (IQueueReader[])state;

            for (int i = 0; i < outstandingReaders.Length; i++)
            {
                outstandingReaders[i].Set(default(Item));
            }
        }

        private static void CompleteWaiters(bool itemAvailable, IQueueWaiter[] waiters)
        {
            for (int i = 0; i < waiters.Length; i++)
            {
                waiters[i].Set(itemAvailable);
            }
        }

        private static void CompleteWaitersFalseCallback(object state)
        {
            CompleteWaiters(false, (IQueueWaiter[])state);
        }

        private static void CompleteWaitersLater(bool itemAvailable, IQueueWaiter[] waiters)
        {
            if (itemAvailable)
            {
                if (s_completeWaitersTrueCallback == null)
                {
                    s_completeWaitersTrueCallback = CompleteWaitersTrueCallback;
                }

                ActionItem.Schedule(s_completeWaitersTrueCallback, waiters);
            }
            else
            {
                if (s_completeWaitersFalseCallback == null)
                {
                    s_completeWaitersFalseCallback = CompleteWaitersFalseCallback;
                }

                ActionItem.Schedule(s_completeWaitersFalseCallback, waiters);
            }
        }

        private static void CompleteWaitersTrueCallback(object state)
        {
            CompleteWaiters(true, (IQueueWaiter[])state);
        }

        private static void InvokeDequeuedCallback(Action dequeuedCallback)
        {
            if (dequeuedCallback != null)
            {
                dequeuedCallback();
            }
        }

        private static void InvokeDequeuedCallbackLater(Action dequeuedCallback)
        {
            if (dequeuedCallback != null)
            {
                if (s_onInvokeDequeuedCallback == null)
                {
                    s_onInvokeDequeuedCallback = OnInvokeDequeuedCallback;
                }

                ActionItem.Schedule(s_onInvokeDequeuedCallback, dequeuedCallback);
            }
        }

        private static void OnDispatchCallback(object state)
        {
            ((InputQueue<T>)state).Dispatch();
        }

        private static void OnInvokeDequeuedCallback(object state)
        {
            Fx.Assert(state != null, "InputQueue.OnInvokeDequeuedCallback: (state != null)");

            Action dequeuedCallback = (Action)state;
            dequeuedCallback();
        }

        private void EnqueueAndDispatch(Item item, bool canDispatchOnThisThread)
        {
            bool disposeItem = false;
            IQueueReader reader = null;
            bool dispatchLater = false;
            IQueueWaiter[] waiters = null;
            bool itemAvailable = true;

            lock (ThisLock)
            {
                itemAvailable = !((_queueState == QueueState.Closed) || (_queueState == QueueState.Shutdown));
                GetWaiters(out waiters);

                if (_queueState == QueueState.Open)
                {
                    if (canDispatchOnThisThread)
                    {
                        if (_readerQueue.Count == 0)
                        {
                            _itemQueue.EnqueueAvailableItem(item);
                        }
                        else
                        {
                            reader = _readerQueue.Dequeue();
                        }
                    }
                    else
                    {
                        if (_readerQueue.Count == 0)
                        {
                            _itemQueue.EnqueueAvailableItem(item);
                        }
                        else
                        {
                            _itemQueue.EnqueuePendingItem(item);
                            dispatchLater = true;
                        }
                    }
                }
                else // queueState == QueueState.Closed || queueState == QueueState.Shutdown
                {
                    disposeItem = true;
                }
            }

            if (waiters != null)
            {
                if (canDispatchOnThisThread)
                {
                    CompleteWaiters(itemAvailable, waiters);
                }
                else
                {
                    CompleteWaitersLater(itemAvailable, waiters);
                }
            }

            if (reader != null)
            {
                InvokeDequeuedCallback(item.DequeuedCallback);
                reader.Set(item);
            }

            if (dispatchLater)
            {
                if (s_onDispatchCallback == null)
                {
                    s_onDispatchCallback = OnDispatchCallback;
                }

                ActionItem.Schedule(s_onDispatchCallback, this);
            }
            else if (disposeItem)
            {
                InvokeDequeuedCallback(item.DequeuedCallback);
                DisposeItem(item);
            }
        }

        // This will not block, however, Dispatch() must be called later if this function
        // returns true.
        private bool EnqueueWithoutDispatch(Item item)
        {
            lock (ThisLock)
            {
                // Open
                if (_queueState != QueueState.Closed && _queueState != QueueState.Shutdown)
                {
                    if (_readerQueue.Count == 0 && _waiterList.Count == 0)
                    {
                        _itemQueue.EnqueueAvailableItem(item);
                        return false;
                    }
                    else
                    {
                        _itemQueue.EnqueuePendingItem(item);
                        return true;
                    }
                }
            }

            DisposeItem(item);
            InvokeDequeuedCallbackLater(item.DequeuedCallback);
            return false;
        }

        private void GetWaiters(out IQueueWaiter[] waiters)
        {
            if (_waiterList.Count > 0)
            {
                waiters = _waiterList.ToArray();
                _waiterList.Clear();
            }
            else
            {
                waiters = null;
            }
        }

        // Used for timeouts. The InputQueue must remove readers from its reader queue to prevent
        // dispatching items to timed out readers.
        private bool RemoveReader(IQueueReader reader)
        {
            Fx.Assert(reader != null, "InputQueue.RemoveReader: (reader != null)");

            lock (ThisLock)
            {
                if (_queueState == QueueState.Open || _queueState == QueueState.Shutdown)
                {
                    bool removed = false;

                    for (int i = _readerQueue.Count; i > 0; i--)
                    {
                        IQueueReader temp = _readerQueue.Dequeue();
                        if (object.ReferenceEquals(temp, reader))
                        {
                            removed = true;
                        }
                        else
                        {
                            _readerQueue.Enqueue(temp);
                        }
                    }

                    return removed;
                }
            }

            return false;
        }

        private enum QueueState
        {
            Open,
            Shutdown,
            Closed
        }

        private interface IQueueReader
        {
            void Set(Item item);
        }

        private interface IQueueWaiter
        {
            void Set(bool itemAvailable);
        }

        private struct Item
        {
            private readonly T _value;

            public Item(T value, Action dequeuedCallback)
                : this(value, null, dequeuedCallback)
            {
            }

            public Item(Exception exception, Action dequeuedCallback)
                : this(null, exception, dequeuedCallback)
            {
            }

            private Item(T value, Exception exception, Action dequeuedCallback)
            {
                _value = value;
                Exception = exception;
                DequeuedCallback = dequeuedCallback;
            }

            public Action DequeuedCallback { get; }

            public Exception Exception { get; }

            public T Value
            {
                get { return _value; }
            }

            public T GetValue()
            {
                if (Exception != null)
                {
                    throw Fx.Exception.AsError(Exception);
                }

                return _value;
            }
        }

        private class ItemQueue
        {
            private int _head;
            private Item[] _items;
            private int _pendingCount;

            public ItemQueue()
            {
                _items = new Item[1];
            }

            public bool HasAnyItem
            {
                get { return ItemCount > 0; }
            }

            public bool HasAvailableItem
            {
                get { return ItemCount > _pendingCount; }
            }

            public int ItemCount { get; private set; }

            public Item DequeueAnyItem()
            {
                if (_pendingCount == ItemCount)
                {
                    _pendingCount--;
                }
                return DequeueItemCore();
            }

            public Item DequeueAvailableItem()
            {
                Fx.AssertAndThrow(ItemCount != _pendingCount, "ItemQueue does not contain any available items");
                return DequeueItemCore();
            }

            public void EnqueueAvailableItem(Item item)
            {
                EnqueueItemCore(item);
            }

            public void EnqueuePendingItem(Item item)
            {
                EnqueueItemCore(item);
                _pendingCount++;
            }

            public void MakePendingItemAvailable()
            {
                Fx.AssertAndThrow(_pendingCount != 0, "ItemQueue does not contain any pending items");
                _pendingCount--;
            }

            private Item DequeueItemCore()
            {
                Fx.AssertAndThrow(ItemCount != 0, "ItemQueue does not contain any items");
                Item item = _items[_head];
                _items[_head] = new Item();
                ItemCount--;
                _head = (_head + 1) % _items.Length;
                return item;
            }

            private void EnqueueItemCore(Item item)
            {
                if (ItemCount == _items.Length)
                {
                    Item[] newItems = new Item[_items.Length * 2];
                    for (int i = 0; i < ItemCount; i++)
                    {
                        newItems[i] = _items[(_head + i) % _items.Length];
                    }
                    _head = 0;
                    _items = newItems;
                }
                int tail = (_head + ItemCount) % _items.Length;
                _items[tail] = item;
                ItemCount++;
            }
        }

        private class WaitQueueReader : IQueueReader
        {
            private Exception _exception;
            private readonly InputQueue<T> _inputQueue;
            private T _item;
            private readonly AsyncManualResetEvent _waitEvent;

            public WaitQueueReader(InputQueue<T> inputQueue)
            {
                _inputQueue = inputQueue;
                _waitEvent = new AsyncManualResetEvent();
            }

            public void Set(Item item)
            {
                lock (this)
                {
                    Fx.Assert(_item == null, "InputQueue.WaitQueueReader.Set: (this.item == null)");
                    Fx.Assert(_exception == null, "InputQueue.WaitQueueReader.Set: (this.exception == null)");

                    _exception = item.Exception;
                    _item = item.Value;
                    _waitEvent.Set();
                }
            }

            public async Task<(T result, bool success)> WaitAsync(CancellationToken token)
            {
                bool isSafeToClose = false;
                try
                {
                    if (!await _waitEvent.WaitAsync(token))
                    {
                        if (_inputQueue.RemoveReader(this))
                        {
                            isSafeToClose = true;
                            return (null, false);
                        }
                        else
                        {
                            await _waitEvent.WaitAsync();
                        }
                    }

                    isSafeToClose = true;
                }
                finally
                {
                    if (isSafeToClose)
                    {
                        _waitEvent.Dispose();
                    }
                }

                if (_exception != null)
                {
                    throw Fx.Exception.AsError(_exception);
                }

                return (_item, true);
            }
        }

        private class WaitQueueWaiter : IQueueWaiter
        {
            private bool _itemAvailable;
            private readonly AsyncManualResetEvent _waitEvent;

            public WaitQueueWaiter()
            {
                _waitEvent = new AsyncManualResetEvent();
            }

            public void Set(bool itemAvailable)
            {
                lock (this)
                {
                    _itemAvailable = itemAvailable;
                    _waitEvent.Set();
                }
            }

            public async Task<bool> WaitAsync(CancellationToken token)
            {
                if (!await _waitEvent.WaitAsync(token))
                {
                    return false;
                }

                return _itemAvailable;
            }
        }
    }
}