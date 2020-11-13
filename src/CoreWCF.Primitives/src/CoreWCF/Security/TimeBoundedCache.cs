using CoreWCF.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace CoreWCF.Security
{
    // NOTE: this class does minimum argument checking as it is all internal 
    class TimeBoundedCache 
    {
        static Action<object> purgeCallback;
        Hashtable entries;
        // if there are less than lowWaterMark entries, no purging is done
        int lowWaterMark;
        DateTime nextPurgeTimeUtc;
        TimeSpan purgeInterval;
        PurgingMode purgingMode;
        IOThreadTimer purgingTimer;
        bool doRemoveNotification;

        protected TimeBoundedCache(int lowWaterMark, int maxCacheItems, IEqualityComparer keyComparer, PurgingMode purgingMode, TimeSpan purgeInterval, bool doRemoveNotification)
        {
            this.entries = new Hashtable(keyComparer);
            this.CacheLock = new ReaderWriterLock();
            this.lowWaterMark = lowWaterMark;
            this.Capacity = maxCacheItems;
            this.purgingMode = purgingMode;
            this.purgeInterval = purgeInterval;
            this.doRemoveNotification = doRemoveNotification;
            this.nextPurgeTimeUtc = DateTime.UtcNow.Add(this.purgeInterval);
        }

        public int Count => this.entries.Count;

        static Action<object> PurgeCallback
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

        protected Hashtable Entries => this.entries;

        protected ReaderWriterLock CacheLock { get; }

        protected bool TryAddItem(object key, object item, DateTime expirationTime, bool replaceExistingEntry)
        {
            return this.TryAddItem(key, new ExpirableItem(item, expirationTime), replaceExistingEntry);
        }

        void CancelTimerIfNeeded()
        {
            if (this.Count == 0 && this.purgingTimer != null)
            {
                this.purgingTimer.Cancel();
                this.purgingTimer = null;
            }
        }

        void StartTimerIfNeeded()
        {
            if (this.purgingMode != PurgingMode.TimerBasedPurge)
            {
                return;
            }
            if (this.purgingTimer == null)
            {
                this.purgingTimer = new IOThreadTimer(PurgeCallback, this, false);
                this.purgingTimer.Set(this.purgeInterval);
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
                    this.CacheLock.AcquireWriterLock(-1);
                    lockHeld = true;
                }
                PurgeIfNeeded();
                EnforceQuota();
                IExpirableItem currentItem = this.entries[key] as IExpirableItem;
                if (currentItem == null || IsExpired(currentItem))
                {
                    this.entries[key] = item;
                }
                else if (!replaceExistingEntry)
                {
                    return false;
                }
                else
                {
                    this.entries[key] = item;
                }
                if (currentItem != null && doRemoveNotification)
                {
                    this.OnRemove(ExtractItem(currentItem));
                }
                StartTimerIfNeeded();
                return true;
            }
            finally
            {
                if (lockHeld)
                {
                    this.CacheLock.ReleaseWriterLock();
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
                    this.CacheLock.AcquireWriterLock(-1);
                    lockHeld = true;
                }
                PurgeIfNeeded();
                EnforceQuota();
                IExpirableItem currentItem = this.entries[key] as IExpirableItem;
                if (currentItem == null || IsExpired(currentItem))
                {
                    return false;
                }
                else
                {
                    this.entries[key] = new ExpirableItem(item, expirationTime);
                    if (currentItem != null && doRemoveNotification)
                    {
                        this.OnRemove(ExtractItem(currentItem));
                    }
                    StartTimerIfNeeded();
                    return true;
                }
            }
            finally
            {
                if (lockHeld)
                {
                    this.CacheLock.ReleaseWriterLock();
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
                    this.CacheLock.AcquireWriterLock(-1);
                    lockHeld = true;
                }

                int count = this.entries.Count;
                if (doRemoveNotification)
                {
                    foreach (IExpirableItem item in this.entries.Values)
                    {
                        OnRemove(ExtractItem(item));
                    }
                }
                this.entries.Clear();
                CancelTimerIfNeeded();
            }
            finally
            {
                if (lockHeld)
                {
                    this.CacheLock.ReleaseWriterLock();
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
                    this.CacheLock.AcquireReaderLock(-1);
                    lockHeld = true;
                }
                IExpirableItem item = this.entries[key] as IExpirableItem;
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
                    this.CacheLock.ReleaseReaderLock();
                }
            }
        }

        protected virtual ArrayList OnQuotaReached(Hashtable cacheTable)
        {
            this.ThrowQuotaReachedException();
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
                    this.CacheLock.AcquireWriterLock(-1);
                    lockHeld = true;
                }
                PurgeIfNeeded();
                IExpirableItem currentItem = this.entries[key] as IExpirableItem;
                bool result = (currentItem != null) && !IsExpired(currentItem);
                if (currentItem != null)
                {
                    this.entries.Remove(key);
                    if (doRemoveNotification)
                    {
                        this.OnRemove(ExtractItem(currentItem));
                    }
                    CancelTimerIfNeeded();
                }
                return result;
            }
            finally
            {
                if (lockHeld)
                {
                    this.CacheLock.ReleaseWriterLock();
                }
            }
        }


        void EnforceQuota()
        {
            if (!(this.CacheLock.IsWriterLockHeld == true))
            {
                // we failfast here because if we don't have the lock we could corrupt the cache
                Fx.Assert("Cache write lock is not held.");
                DiagnosticUtility.FailFast("Cache write lock is not held.");
            }
            if (this.Count >= this.Capacity)
            {
                ArrayList keysToBeRemoved;
                keysToBeRemoved = this.OnQuotaReached(this.entries);
                if (keysToBeRemoved != null)
                {
                    for (int i = 0; i < keysToBeRemoved.Count; ++i)
                    {
                        this.entries.Remove(keysToBeRemoved[i]);
                    }
                    
                }
                CancelTimerIfNeeded();
                if (this.Count >= this.Capacity)
                {
                    this.ThrowQuotaReachedException();
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

        bool IsExpired(IExpirableItem item)
        {
            Fx.Assert(item.ExpirationTime == DateTime.MaxValue || item.ExpirationTime.Kind == DateTimeKind.Utc, "");
            return (item.ExpirationTime <= DateTime.UtcNow);
        }

        bool ShouldPurge()
        {
            if (this.Count >= this.Capacity)
            {
                return true;
            }
            else if (this.purgingMode == PurgingMode.AccessBasedPurge && DateTime.UtcNow > this.nextPurgeTimeUtc && this.Count > this.lowWaterMark)
            {
                return true;
            }
            else 
            {
                return false;
            }
        }

        void PurgeIfNeeded()
        {
            if (!(this.CacheLock.IsWriterLockHeld == true))
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
        void PurgeStaleItems()
        {
            if (!(this.CacheLock.IsWriterLockHeld == true))
            {
                // we failfast here because if we don't have the lock we could corrupt the cache
                Fx.Assert("Cache write lock is not held.");
                DiagnosticUtility.FailFast("Cache write lock is not held.");
            }
            ArrayList expiredItems = new ArrayList();
            foreach (object key in this.entries.Keys)
            {
                IExpirableItem item = this.entries[key] as IExpirableItem;
                if (IsExpired(item))
                {
                    // this is a stale item. Remove!
                    this.OnRemove(ExtractItem(item));
                    expiredItems.Add(key);
                }
            }
            for (int i = 0; i < expiredItems.Count; ++i)
            {
                this.entries.Remove(expiredItems[i]);
            }
            CancelTimerIfNeeded();
            this.nextPurgeTimeUtc = DateTime.UtcNow.Add(this.purgeInterval);
        }

        void ThrowQuotaReachedException()
        {
            string message = SR.Format(SR.CacheQuotaReached, this.Capacity);
            Exception inner = new QuotaExceededException(message);
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(message, inner));
        }

        static void PurgeCallbackStatic(object state)
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
            static ExpirableItemComparer instance;

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
            object item;

            public ExpirableItem(object item, DateTime expirationTime)
            {
                this.item = item;
                Fx.Assert( expirationTime == DateTime.MaxValue || expirationTime.Kind == DateTimeKind.Utc, "");
                this.ExpirationTime = expirationTime;
            }

            public DateTime ExpirationTime { get; }
            public object Item => this.item;
        }
    }

    enum PurgingMode
    {
        TimerBasedPurge,
        AccessBasedPurge
    }
}
