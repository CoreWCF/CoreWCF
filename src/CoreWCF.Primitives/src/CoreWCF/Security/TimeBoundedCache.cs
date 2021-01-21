// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using CoreWCF.Runtime;

namespace CoreWCF.Security
{
    // NOTE: this class does minimum argument checking as it is all internal 
    internal class TimeBoundedCache
    {
        private static Action<object> purgeCallback;
        private readonly Hashtable entries;

        // if there are less than lowWaterMark entries, no purging is done
        private readonly int lowWaterMark;
        private DateTime nextPurgeTimeUtc;
        private TimeSpan purgeInterval;
        private readonly PurgingMode purgingMode;
        private IOThreadTimer purgingTimer;
        private readonly bool doRemoveNotification;

        protected TimeBoundedCache(int lowWaterMark, int maxCacheItems, IEqualityComparer keyComparer, PurgingMode purgingMode, TimeSpan purgeInterval, bool doRemoveNotification)
        {
            entries = new Hashtable(keyComparer);
            CacheLock = new ReaderWriterLock();
            this.lowWaterMark = lowWaterMark;
            Capacity = maxCacheItems;
            this.purgingMode = purgingMode;
            this.purgeInterval = purgeInterval;
            this.doRemoveNotification = doRemoveNotification;
            nextPurgeTimeUtc = DateTime.UtcNow.Add(this.purgeInterval);
        }

        public int Count => entries.Count;

        private static Action<object> PurgeCallback
        {
            get
            {
                if (purgeCallback == null)
                {
                    purgeCallback = new Action<object>(PurgeCallbackStatic);
                }
                return purgeCallback;
            }
        }

        protected int Capacity { get; }

        protected Hashtable Entries => entries;

        protected ReaderWriterLock CacheLock { get; }

        protected bool TryAddItem(object key, object item, DateTime expirationTime, bool replaceExistingEntry)
        {
            return TryAddItem(key, new ExpirableItem(item, expirationTime), replaceExistingEntry);
        }

        private void CancelTimerIfNeeded()
        {
            if (Count == 0 && purgingTimer != null)
            {
                purgingTimer.Cancel();
                purgingTimer = null;
            }
        }

        private void StartTimerIfNeeded()
        {
            if (purgingMode != PurgingMode.TimerBasedPurge)
            {
                return;
            }
            if (purgingTimer == null)
            {
                purgingTimer = new IOThreadTimer(PurgeCallback, this, false);
                purgingTimer.Set(purgeInterval);
            }
        }

        protected bool TryAddItem(object key, IExpirableItem item, bool replaceExistingEntry)
        {
            bool lockHeld = false;
            try
            {
                try { }
                finally
                {
                    CacheLock.AcquireWriterLock(-1);
                    lockHeld = true;
                }
                PurgeIfNeeded();
                EnforceQuota();
                IExpirableItem currentItem = entries[key] as IExpirableItem;
                if (currentItem == null || IsExpired(currentItem))
                {
                    entries[key] = item;
                }
                else if (!replaceExistingEntry)
                {
                    return false;
                }
                else
                {
                    entries[key] = item;
                }
                if (currentItem != null && doRemoveNotification)
                {
                    OnRemove(ExtractItem(currentItem));
                }
                StartTimerIfNeeded();
                return true;
            }
            finally
            {
                if (lockHeld)
                {
                    CacheLock.ReleaseWriterLock();
                }
            }
        }

        protected bool TryReplaceItem(object key, object item, DateTime expirationTime)
        {
            bool lockHeld = false;
            try
            {
                try { }
                finally
                {
                    CacheLock.AcquireWriterLock(-1);
                    lockHeld = true;
                }
                PurgeIfNeeded();
                EnforceQuota();
                IExpirableItem currentItem = entries[key] as IExpirableItem;
                if (currentItem == null || IsExpired(currentItem))
                {
                    return false;
                }
                else
                {
                    entries[key] = new ExpirableItem(item, expirationTime);
                    if (currentItem != null && doRemoveNotification)
                    {
                        OnRemove(ExtractItem(currentItem));
                    }
                    StartTimerIfNeeded();
                    return true;
                }
            }
            finally
            {
                if (lockHeld)
                {
                    CacheLock.ReleaseWriterLock();
                }
            }
        }

        protected void ClearItems()
        {
            bool lockHeld = false;
            try
            {
                try { }
                finally
                {
                    CacheLock.AcquireWriterLock(-1);
                    lockHeld = true;
                }

                int count = entries.Count;
                if (doRemoveNotification)
                {
                    foreach (IExpirableItem item in entries.Values)
                    {
                        OnRemove(ExtractItem(item));
                    }
                }
                entries.Clear();
                CancelTimerIfNeeded();
            }
            finally
            {
                if (lockHeld)
                {
                    CacheLock.ReleaseWriterLock();
                }
            }
        }

        protected object GetItem(object key)
        {
            bool lockHeld = false;
            try
            {
                try { }
                finally
                {
                    CacheLock.AcquireReaderLock(-1);
                    lockHeld = true;
                }
                IExpirableItem item = entries[key] as IExpirableItem;
                if (item == null)
                {
                    return null;
                }
                else if (IsExpired(item))
                {
                    // this is a stale item
                    return null;
                }
                else
                {
                    return ExtractItem(item);
                }
            }
            finally
            {
                if (lockHeld)
                {
                    CacheLock.ReleaseReaderLock();
                }
            }
        }

        protected virtual ArrayList OnQuotaReached(Hashtable cacheTable)
        {
            ThrowQuotaReachedException();
            return null;
        }

        protected virtual void OnRemove(object item)
        {
        }

        protected bool TryRemoveItem(object key)
        {
            bool lockHeld = false;
            try
            {
                try { }
                finally
                {
                    CacheLock.AcquireWriterLock(-1);
                    lockHeld = true;
                }
                PurgeIfNeeded();
                IExpirableItem currentItem = entries[key] as IExpirableItem;
                bool result = (currentItem != null) && !IsExpired(currentItem);
                if (currentItem != null)
                {
                    entries.Remove(key);
                    if (doRemoveNotification)
                    {
                        OnRemove(ExtractItem(currentItem));
                    }
                    CancelTimerIfNeeded();
                }
                return result;
            }
            finally
            {
                if (lockHeld)
                {
                    CacheLock.ReleaseWriterLock();
                }
            }
        }

        private void EnforceQuota()
        {
            if (!(CacheLock.IsWriterLockHeld == true))
            {
                // we failfast here because if we don't have the lock we could corrupt the cache
                Fx.Assert("Cache write lock is not held.");
                DiagnosticUtility.FailFast("Cache write lock is not held.");
            }
            if (Count >= Capacity)
            {
                ArrayList keysToBeRemoved;
                keysToBeRemoved = OnQuotaReached(entries);
                if (keysToBeRemoved != null)
                {
                    for (int i = 0; i < keysToBeRemoved.Count; ++i)
                    {
                        entries.Remove(keysToBeRemoved[i]);
                    }

                }
                CancelTimerIfNeeded();
                if (Count >= Capacity)
                {
                    ThrowQuotaReachedException();
                }
            }
        }

        protected object ExtractItem(IExpirableItem val)
        {
            ExpirableItem wrapper = (val as ExpirableItem);
            if (wrapper != null)
            {
                return wrapper.Item;
            }
            else
            {
                return val;
            }
        }

        private bool IsExpired(IExpirableItem item)
        {
            Fx.Assert(item.ExpirationTime == DateTime.MaxValue || item.ExpirationTime.Kind == DateTimeKind.Utc, "");
            return (item.ExpirationTime <= DateTime.UtcNow);
        }

        private bool ShouldPurge()
        {
            if (Count >= Capacity)
            {
                return true;
            }
            else if (purgingMode == PurgingMode.AccessBasedPurge && DateTime.UtcNow > nextPurgeTimeUtc && Count > lowWaterMark)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private void PurgeIfNeeded()
        {
            if (!(CacheLock.IsWriterLockHeld == true))
            {
                // we failfast here because if we don't have the lock we could corrupt the cache
                Fx.Assert("Cache write lock is not held.");
                DiagnosticUtility.FailFast("Cache write lock is not held.");
            }
            if (ShouldPurge())
            {
                PurgeStaleItems();
            }
        }

        /// <summary>
        /// This method must be called from within a writer lock
        /// </summary>
        private void PurgeStaleItems()
        {
            if (!(CacheLock.IsWriterLockHeld == true))
            {
                // we failfast here because if we don't have the lock we could corrupt the cache
                Fx.Assert("Cache write lock is not held.");
                DiagnosticUtility.FailFast("Cache write lock is not held.");
            }
            ArrayList expiredItems = new ArrayList();
            foreach (object key in entries.Keys)
            {
                IExpirableItem item = entries[key] as IExpirableItem;
                if (IsExpired(item))
                {
                    // this is a stale item. Remove!
                    OnRemove(ExtractItem(item));
                    expiredItems.Add(key);
                }
            }
            for (int i = 0; i < expiredItems.Count; ++i)
            {
                entries.Remove(expiredItems[i]);
            }
            CancelTimerIfNeeded();
            nextPurgeTimeUtc = DateTime.UtcNow.Add(purgeInterval);
        }

        private void ThrowQuotaReachedException()
        {
            string message = SR.Format(SR.CacheQuotaReached, Capacity);
            Exception inner = new QuotaExceededException(message);
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(message, inner));
        }

        private static void PurgeCallbackStatic(object state)
        {
            TimeBoundedCache self = (TimeBoundedCache)state;

            bool lockHeld = false;
            try
            {
                try { }
                finally
                {
                    self.CacheLock.AcquireWriterLock(-1);
                    lockHeld = true;
                }

                if (self.purgingTimer == null)
                {
                    return;
                }
                self.PurgeStaleItems();
                if (self.Count > 0 && self.purgingTimer != null)
                {
                    self.purgingTimer.Set(self.purgeInterval);
                }
            }
            finally
            {
                if (lockHeld)
                {
                    self.CacheLock.ReleaseWriterLock();
                }
            }
        }

        internal interface IExpirableItem
        {
            DateTime ExpirationTime { get; }
        }

        internal class ExpirableItemComparer : IComparer<IExpirableItem>
        {
            private static ExpirableItemComparer instance;

            public static ExpirableItemComparer Default
            {
                get
                {
                    if (instance == null)
                    {
                        instance = new ExpirableItemComparer();
                    }
                    return instance;
                }
            }

            // positive, if item1 will expire before item2. 
            public int Compare(IExpirableItem item1, IExpirableItem item2)
            {
                if (ReferenceEquals(item1, item2))
                {
                    return 0;
                }
                Fx.Assert(item1.ExpirationTime.Kind == item2.ExpirationTime.Kind, "");
                if (item1.ExpirationTime < item2.ExpirationTime)
                {
                    return 1;
                }
                else if (item1.ExpirationTime > item2.ExpirationTime)
                {
                    return -1;
                }
                else
                {
                    return 0;
                }
            }
        }

        internal sealed class ExpirableItem : IExpirableItem
        {
            private readonly object item;

            public ExpirableItem(object item, DateTime expirationTime)
            {
                this.item = item;
                Fx.Assert(expirationTime == DateTime.MaxValue || expirationTime.Kind == DateTimeKind.Utc, "");
                ExpirationTime = expirationTime;
            }

            public DateTime ExpirationTime { get; }
            public object Item => item;
        }
    }

    internal enum PurgingMode
    {
        TimerBasedPurge,
        AccessBasedPurge
    }
}
