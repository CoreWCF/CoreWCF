using System.Collections.Generic;
using CoreWCF.Runtime;

namespace CoreWCF.Collections.Generic
{
    class MruCache<TKey, TValue>
        where TKey : class
        where TValue : class
    {
        LinkedList<TKey> _mruList;
        Dictionary<TKey, CacheEntry> _items;
        int _lowWatermark;
        int _highWatermark;
        CacheEntry _mruEntry;

        public MruCache(int watermark)
            : this(watermark * 4 / 5, watermark)
        {
        }

        //
        // The cache will grow until the high watermark. At which point, the least recently used items
        // will be purge until the cache's size is reduced to low watermark
        //
        public MruCache(int lowWatermark, int highWatermark)
            : this(lowWatermark, highWatermark, null)
        {
        }

        public MruCache(int lowWatermark, int highWatermark, IEqualityComparer<TKey> comparer)
        {
            Fx.Assert(lowWatermark < highWatermark, "");
            Fx.Assert(lowWatermark >= 0, "");

            _lowWatermark = lowWatermark;
            _highWatermark = highWatermark;
            _mruList = new LinkedList<TKey>();
            if (comparer == null)
            {
                _items = new Dictionary<TKey, CacheEntry>();
            }
            else
            {
                _items = new Dictionary<TKey, CacheEntry>(comparer);
            }
        }

        public int Count
        {
            get
            {
                return _items.Count;
            }
        }

        public void Add(TKey key, TValue value)
        {
            Fx.Assert(null != key, "");

            // if anything goes wrong (duplicate entry, etc) we should 
            // clear our caches so that we don't get out of sync
            bool success = false;
            try
            {
                if (_items.Count == _highWatermark)
                {
                    // If the cache is full, purge enough LRU items to shrink the 
                    // cache down to the low watermark
                    int countToPurge = _highWatermark - _lowWatermark;
                    for (int i = 0; i < countToPurge; i++)
                    {
                        TKey keyRemove = _mruList.Last.Value;
                        _mruList.RemoveLast();
                        TValue item = _items[keyRemove].value;
                        _items.Remove(keyRemove);
                        OnSingleItemRemoved(item);
                        OnItemAgedOutOfCache(item);
                    }
                }
                // Add  the new entry to the cache and make it the MRU element
                CacheEntry entry;
                entry.node = _mruList.AddFirst(key);
                entry.value = value;
                _items.Add(key, entry);
                _mruEntry = entry;
                success = true;
            }
            finally
            {
                if (!success)
                {
                    Clear();
                }
            }
        }

        public void Clear()
        {
            _mruList.Clear();
            _items.Clear();
            _mruEntry.value = null;
            _mruEntry.node = null;
        }

        public bool Remove(TKey key)
        {
            Fx.Assert(null != key, "");

            CacheEntry entry;
            if (_items.TryGetValue(key, out entry))
            {
                _items.Remove(key);
                OnSingleItemRemoved(entry.value);
                _mruList.Remove(entry.node);
                if (ReferenceEquals(_mruEntry.node, entry.node))
                {
                    _mruEntry.value = null;
                    _mruEntry.node = null;
                }
                return true;
            }

            return false;
        }

        protected virtual void OnSingleItemRemoved(TValue item)
        {
        }

        protected virtual void OnItemAgedOutOfCache(TValue item)
        {
        }

        //
        // If found, make the entry most recently used
        //
        public bool TryGetValue(TKey key, out TValue value)
        {
            // first check our MRU item
            if (_mruEntry.node != null && key != null && key.Equals(_mruEntry.node.Value))
            {
                value = _mruEntry.value;
                return true;
            }

            CacheEntry entry;

            bool found = _items.TryGetValue(key, out entry);
            value = entry.value;

            // Move the node to the head of the MRU list if it's not already there
            if (found && _mruList.Count > 1
                && !ReferenceEquals(_mruList.First, entry.node))
            {
                _mruList.Remove(entry.node);
                _mruList.AddFirst(entry.node);
                _mruEntry = entry;
            }

            return found;
        }

        struct CacheEntry
        {
            internal TValue value;
            internal LinkedListNode<TKey> node;
        }
    }

}