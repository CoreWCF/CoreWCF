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
        private static Action<object> completeOutstandingReadersCallback;
        private static Action<object> completeWaitersFalseCallback;
        private static Action<object> completeWaitersTrueCallback;
        private static Action<object> onDispatchCallback;
        private static Action<object> onInvokeDequeuedCallback;
        private QueueState queueState;
        private readonly ItemQueue itemQueue;
        private readonly Queue<IQueueReader> readerQueue;
        private readonly List<IQueueWaiter> waiterList;

        public InputQueue()
        {
            itemQueue = new ItemQueue();
            readerQueue = new Queue<IQueueReader>();
            waiterList = new List<IQueueWaiter>();
            queueState = QueueState.Open;
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
                    return itemQueue.ItemCount;
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
            get { return itemQueue; }
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
                if (queueState == QueueState.Open)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        item = itemQueue.DequeueAvailableItem();
                    }
                    else
                    {
                        reader = new WaitQueueReader(this);
                        readerQueue.Enqueue(reader);
                    }
                }
                else if (queueState == QueueState.Shutdown)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        item = itemQueue.DequeueAvailableItem();
                    }
                    else if (itemQueue.HasAnyItem)
                    {
                        reader = new WaitQueueReader(this);
                        readerQueue.Enqueue(reader);
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
                itemAvailable = !((queueState == QueueState.Closed) || (queueState == QueueState.Shutdown));
                GetWaiters(out waiters);

                if (queueState != QueueState.Closed)
                {
                    itemQueue.MakePendingItemAvailable();

                    if (readerQueue.Count > 0)
                    {
                        item = itemQueue.DequeueAvailableItem();
                        reader = readerQueue.Dequeue();

                        if (queueState == QueueState.Shutdown && readerQueue.Count > 0 && itemQueue.ItemCount == 0)
                        {
                            outstandingReaders = new IQueueReader[readerQueue.Count];
                            readerQueue.CopyTo(outstandingReaders, 0);
                            readerQueue.Clear();

                            itemAvailable = false;
                        }
                    }
                }
            }

            if (outstandingReaders != null)
            {
                if (completeOutstandingReadersCallback == null)
                {
                    completeOutstandingReadersCallback = CompleteOutstandingReadersCallback;
                }

                ActionItem.Schedule(completeOutstandingReadersCallback, outstandingReaders);
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
                if (queueState == QueueState.Shutdown)
                {
                    return;
                }

                if (queueState == QueueState.Closed)
                {
                    return;
                }

                queueState = QueueState.Shutdown;

                if (readerQueue.Count > 0 && itemQueue.ItemCount == 0)
                {
                    outstandingReaders = new IQueueReader[readerQueue.Count];
                    readerQueue.CopyTo(outstandingReaders, 0);
                    readerQueue.Clear();
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
                if (queueState == QueueState.Open)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        itemAvailable = true;
                    }
                    else
                    {
                        waiter = new WaitQueueWaiter();
                        waiterList.Add(waiter);
                    }
                }
                else if (queueState == QueueState.Shutdown)
                {
                    if (itemQueue.HasAvailableItem)
                    {
                        itemAvailable = true;
                    }
                    else if (itemQueue.HasAnyItem)
                    {
                        waiter = new WaitQueueWaiter();
                        waiterList.Add(waiter);
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
                if (queueState != QueueState.Closed)
                {
                    queueState = QueueState.Closed;
                    dispose = true;
                }
            }

            if (dispose)
            {
                while (readerQueue.Count > 0)
                {
                    IQueueReader reader = readerQueue.Dequeue();
                    reader.Set(default(Item));
                }

                while (itemQueue.HasAnyItem)
                {
                    Item item = itemQueue.DequeueAnyItem();
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
                if (completeWaitersTrueCallback == null)
                {
                    completeWaitersTrueCallback = CompleteWaitersTrueCallback;
                }

                ActionItem.Schedule(completeWaitersTrueCallback, waiters);
            }
            else
            {
                if (completeWaitersFalseCallback == null)
                {
                    completeWaitersFalseCallback = CompleteWaitersFalseCallback;
                }

                ActionItem.Schedule(completeWaitersFalseCallback, waiters);
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
                if (onInvokeDequeuedCallback == null)
                {
                    onInvokeDequeuedCallback = OnInvokeDequeuedCallback;
                }

                ActionItem.Schedule(onInvokeDequeuedCallback, dequeuedCallback);
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
                itemAvailable = !((queueState == QueueState.Closed) || (queueState == QueueState.Shutdown));
                GetWaiters(out waiters);

                if (queueState == QueueState.Open)
                {
                    if (canDispatchOnThisThread)
                    {
                        if (readerQueue.Count == 0)
                        {
                            itemQueue.EnqueueAvailableItem(item);
                        }
                        else
                        {
                            reader = readerQueue.Dequeue();
                        }
                    }
                    else
                    {
                        if (readerQueue.Count == 0)
                        {
                            itemQueue.EnqueueAvailableItem(item);
                        }
                        else
                        {
                            itemQueue.EnqueuePendingItem(item);
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
                if (onDispatchCallback == null)
                {
                    onDispatchCallback = OnDispatchCallback;
                }

                ActionItem.Schedule(onDispatchCallback, this);
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
                if (queueState != QueueState.Closed && queueState != QueueState.Shutdown)
                {
                    if (readerQueue.Count == 0 && waiterList.Count == 0)
                    {
                        itemQueue.EnqueueAvailableItem(item);
                        return false;
                    }
                    else
                    {
                        itemQueue.EnqueuePendingItem(item);
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
            if (waiterList.Count > 0)
            {
                waiters = waiterList.ToArray();
                waiterList.Clear();
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
                if (queueState == QueueState.Open || queueState == QueueState.Shutdown)
                {
                    bool removed = false;

                    for (int i = readerQueue.Count; i > 0; i--)
                    {
                        IQueueReader temp = readerQueue.Dequeue();
                        if (object.ReferenceEquals(temp, reader))
                        {
                            removed = true;
                        }
                        else
                        {
                            readerQueue.Enqueue(temp);
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
            private readonly Action dequeuedCallback;
            private readonly Exception exception;
            private readonly T value;

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
                this.value = value;
                this.exception = exception;
                this.dequeuedCallback = dequeuedCallback;
            }

            public Action DequeuedCallback
            {
                get { return dequeuedCallback; }
            }

            public Exception Exception
            {
                get { return exception; }
            }

            public T Value
            {
                get { return value; }
            }

            public T GetValue()
            {
                if (exception != null)
                {
                    throw Fx.Exception.AsError(exception);
                }

                return value;
            }
        }

        private class ItemQueue
        {
            private int head;
            private Item[] items;
            private int pendingCount;
            private int totalCount;

            public ItemQueue()
            {
                items = new Item[1];
            }

            public bool HasAnyItem
            {
                get { return totalCount > 0; }
            }

            public bool HasAvailableItem
            {
                get { return totalCount > pendingCount; }
            }

            public int ItemCount
            {
                get { return totalCount; }
            }

            public Item DequeueAnyItem()
            {
                if (pendingCount == totalCount)
                {
                    pendingCount--;
                }
                return DequeueItemCore();
            }

            public Item DequeueAvailableItem()
            {
                Fx.AssertAndThrow(totalCount != pendingCount, "ItemQueue does not contain any available items");
                return DequeueItemCore();
            }

            public void EnqueueAvailableItem(Item item)
            {
                EnqueueItemCore(item);
            }

            public void EnqueuePendingItem(Item item)
            {
                EnqueueItemCore(item);
                pendingCount++;
            }

            public void MakePendingItemAvailable()
            {
                Fx.AssertAndThrow(pendingCount != 0, "ItemQueue does not contain any pending items");
                pendingCount--;
            }

            private Item DequeueItemCore()
            {
                Fx.AssertAndThrow(totalCount != 0, "ItemQueue does not contain any items");
                Item item = items[head];
                items[head] = new Item();
                totalCount--;
                head = (head + 1) % items.Length;
                return item;
            }

            private void EnqueueItemCore(Item item)
            {
                if (totalCount == items.Length)
                {
                    Item[] newItems = new Item[items.Length * 2];
                    for (int i = 0; i < totalCount; i++)
                    {
                        newItems[i] = items[(head + i) % items.Length];
                    }
                    head = 0;
                    items = newItems;
                }
                int tail = (head + totalCount) % items.Length;
                items[tail] = item;
                totalCount++;
            }
        }

        private class WaitQueueReader : IQueueReader
        {
            private Exception exception;
            private readonly InputQueue<T> inputQueue;
            private T item;
            private readonly AsyncManualResetEvent waitEvent;

            public WaitQueueReader(InputQueue<T> inputQueue)
            {
                this.inputQueue = inputQueue;
                waitEvent = new AsyncManualResetEvent();
            }

            public void Set(Item item)
            {
                lock (this)
                {
                    Fx.Assert(this.item == null, "InputQueue.WaitQueueReader.Set: (this.item == null)");
                    Fx.Assert(exception == null, "InputQueue.WaitQueueReader.Set: (this.exception == null)");

                    exception = item.Exception;
                    this.item = item.Value;
                    waitEvent.Set();
                }
            }

            public async Task<(T result, bool success)> WaitAsync(CancellationToken token)
            {
                bool isSafeToClose = false;
                try
                {
                    if (!await waitEvent.WaitAsync(token))
                    {
                        if (inputQueue.RemoveReader(this))
                        {
                            isSafeToClose = true;
                            return (null, false);
                        }
                        else
                        {
                            await waitEvent.WaitAsync();
                        }
                    }

                    isSafeToClose = true;
                }
                finally
                {
                    if (isSafeToClose)
                    {
                        waitEvent.Dispose();
                    }
                }

                if (exception != null)
                {
                    throw Fx.Exception.AsError(exception);
                }

                return (item, true);
            }
        }

        private class WaitQueueWaiter : IQueueWaiter
        {
            private bool itemAvailable;
            private readonly AsyncManualResetEvent waitEvent;

            public WaitQueueWaiter()
            {
                waitEvent = new AsyncManualResetEvent();
            }

            public void Set(bool itemAvailable)
            {
                lock (this)
                {
                    this.itemAvailable = itemAvailable;
                    waitEvent.Set();
                }
            }

            public async Task<bool> WaitAsync(CancellationToken token)
            {
                if (!await waitEvent.WaitAsync(token))
                {
                    return false;
                }

                return itemAvailable;
            }
        }
    }

}