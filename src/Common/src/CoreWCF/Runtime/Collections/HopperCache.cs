// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Threading;

namespace CoreWCF.Runtime.Collections
{
    // This cache works like a MruCache, but operates loosely and without locks in the mainline path.
    //
    // It consists of three 'hoppers', which are Hashtables (chosen for their nice threading characteristics - reading
    // doesn't require a lock).  Items enter the cache in the second hopper.  On lookups, cache hits result in the
    // cache entry being promoted to the first hopper.  When the first hopper is full, the third hopper is dropped,
    // and the first and second hoppers are shifted down, leaving an empty first hopper.  If the second hopper is
    // full when a new cache entry is added, the third hopper is dropped, the second hopper is shifted down, and a
    // new second hopper is slotted in to become the new item entrypoint.
    //
    // Items can only be added and looked up.  There's no way to remove an item besides through attrition.
    //
    // This cache has a built-in concept of weakly-referenced items (which can be enabled or disabled in the
    // constructor).  It needs this concept since the caller of the cache can't remove dead cache items itself.
    // A weak HopperCache will simply ignore dead entries.
    //
    // This structure allows cache lookups to be almost lock-free.  The only time the first hopper is written to
    // is when a cache entry is promoted.  Promoting a cache entry is not critical - it's ok to skip a promotion.
    // Only one promotion is allowed at a time.  If a second is attempted, it is skipped.  This allows promotions
    // to be synchronized with just an Interlocked call.
    //
    // New cache entries go into the second hopper, which requires a lock, as does shifting the hoppers down.
    //
    // The hopperSize parameter determines the size of the first hopper.  When it reaches this size, the hoppers
    // are shifted.  The second hopper is allowed to grow to twice this size.  This is because it needs room to get
    // new cache entries into the system, and the second hopper typically starts out 'full'.  Entries are never added
    // directly to the third hopper.
    //
    // It's a error on the part of the caller to add the same key to the cache again if it's already in the cache
    // with a different value.  The new value will not necessarily overwrite the old value.
    //
    // If a cache entry is about to be promoted from the third hopper, and in the mean time the third hopper has been
    // shifted away, an intervening GetValue for the same key might return null, even though the item is still in
    // the cache and a later GetValue might find it.  So it's very important never to add the same key to the cache
    // with two different values, even if GetValue returns null for the key in-between the first add and the second.
    // (If this particular behavior is a problem, it may be possible to tighten up, but it's not necessary for the
    // current use of HopperCache - UriPrefixTable.)
    internal class HopperCache
    {
        private readonly int hopperSize;
        private readonly bool weak;
        private Hashtable outstandingHopper;
        private Hashtable strongHopper;
        private Hashtable limitedHopper;
        private int promoting;
        private LastHolder mruEntry;


        public HopperCache(int hopperSize, bool weak)
        {
            Fx.Assert(hopperSize > 0, "HopperCache hopperSize must be positive.");

            this.hopperSize = hopperSize;
            this.weak = weak;

            outstandingHopper = new Hashtable(hopperSize * 2);
            strongHopper = new Hashtable(hopperSize * 2);
            limitedHopper = new Hashtable(hopperSize * 2);
        }

        // Calls to Add must be synchronized.
        public void Add(object key, object value)
        {
            Fx.Assert(key != null, "HopperCache key cannot be null.");
            Fx.Assert(value != null, "HopperCache value cannot be null.");

            // Special-case DBNull since it can never be collected.
            if (weak && !object.ReferenceEquals(value, DBNull.Value))
            {
                value = new WeakReference(value);
            }

            Fx.Assert(strongHopper.Count <= hopperSize * 2,
                "HopperCache strongHopper is bigger than it's allowed to get.");

            if (strongHopper.Count >= hopperSize * 2)
            {
                Hashtable recycled = limitedHopper;
                recycled.Clear();
                recycled.Add(key, value);

                // The try/finally is here to make sure these happen without interruption.
                try { }
                finally
                {
                    limitedHopper = strongHopper;
                    strongHopper = recycled;
                }
            }
            else
            {
                // We do nothing to prevent things from getting added multiple times.  Also may be writing over
                // a dead weak entry.
                strongHopper[key] = value;
            }
        }

        // Calls to GetValue do not need to be synchronized, but the object used to synchronize the Add calls
        // must be passed in.  It's sometimes used.
        public object GetValue(object syncObject, object key)
        {
            Fx.Assert(key != null, "Can't look up a null key.");

            WeakReference weakRef;
            object value;

            // The MruCache does this so we have to too.
            LastHolder last = mruEntry;
            if (last != null && key.Equals(last.Key))
            {
                if (weak && (weakRef = last.Value as WeakReference) != null)
                {
                    value = weakRef.Target;
                    if (value != null)
                    {
                        return value;
                    }
                    mruEntry = null;
                }
                else
                {
                    return last.Value;
                }
            }

            // Try the first hopper.
            object origValue = outstandingHopper[key];
            value = weak && (weakRef = origValue as WeakReference) != null ? weakRef.Target : origValue;
            if (value != null)
            {
                mruEntry = new LastHolder(key, origValue);
                return value;
            }

            // Try the subsequent hoppers.
            origValue = strongHopper[key];
            value = weak && (weakRef = origValue as WeakReference) != null ? weakRef.Target : origValue;
            if (value == null)
            {
                origValue = limitedHopper[key];
                value = weak && (weakRef = origValue as WeakReference) != null ? weakRef.Target : origValue;
                if (value == null)
                {
                    // Still no value?  It's not here.
                    return null;
                }
            }

            mruEntry = new LastHolder(key, origValue);

            // If we can get the promoting semaphore, move up to the outstanding hopper.
            int wasPromoting = 1;
            try
            {
                try { }
                finally
                {
                    // This is effectively a lock, which is why it uses lock semantics.  If the Interlocked call
                    // were 'lost', the cache wouldn't deadlock, but it would be permanently broken.
                    wasPromoting = Interlocked.CompareExchange(ref promoting, 1, 0);
                }

                // Only one thread can be inside this 'if' at a time.
                if (wasPromoting == 0)
                {
                    Fx.Assert(outstandingHopper.Count <= hopperSize,
                        "HopperCache outstandingHopper is bigger than it's allowed to get.");

                    if (outstandingHopper.Count >= hopperSize)
                    {
                        lock (syncObject)
                        {
                            Hashtable recycled = limitedHopper;
                            recycled.Clear();
                            recycled.Add(key, origValue);

                            // The try/finally is here to make sure these happen without interruption.
                            try { }
                            finally
                            {
                                limitedHopper = strongHopper;
                                strongHopper = outstandingHopper;
                                outstandingHopper = recycled;
                            }
                        }
                    }
                    else
                    {
                        // It's easy for this to happen twice with the same key.
                        //
                        // It's important that no one else can be shifting the current oustandingHopper
                        // during this operation.  We are only allowed to modify the *current* outstandingHopper
                        // while holding the pseudo-lock, which would be violated if it could be shifted out from
                        // under us (and potentially added to by Add in a race).
                        outstandingHopper[key] = origValue;
                    }
                }
            }
            finally
            {
                if (wasPromoting == 0)
                {
                    promoting = 0;
                }
            }

            return value;
        }

        private class LastHolder
        {
            private readonly object value;

            internal LastHolder(object key, object value)
            {
                Key = key;
                this.value = value;
            }

            internal object Key { get; private set; }

            internal object Value
            {
                get
                {
                    return value;
                }
            }
        }
    }

}