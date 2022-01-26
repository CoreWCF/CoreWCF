// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;

namespace CoreWCF.Dispatcher
{
    internal class NameValueCache<T>
    {
        // The NameValueCache implements a structure that uses a dictionary to map objects to
        // indices of an array of cache entries.  This allows us to store the cache entries in 
        // the order in which they were added to the cache, and yet still lookup any cache entry.
        // The eviction policy of the cache is to evict the least-recently-added cache entry.  
        // Using a pointer to the next available cache entry in the array, we can always be sure 
        // that the given entry is the oldest entry. 
        private readonly Hashtable _cache;
        private readonly string[] _currentKeys;
        private int _nextAvailableCacheIndex;
        internal const int MaxNumberofEntriesInCache = 16;

        public NameValueCache()
            : this(MaxNumberofEntriesInCache)
        {
        }

        public NameValueCache(int maxCacheEntries)
        {
            _cache = new Hashtable();
            _currentKeys = new string[maxCacheEntries];
        }

        public T Lookup(string key)
        {
            return (T)_cache[key];
        }

        public void AddOrUpdate(string key, T value)
        {
            lock (_cache)
            {
                if (!_cache.ContainsKey(key))
                {
                    if (!string.IsNullOrEmpty(_currentKeys[_nextAvailableCacheIndex]))
                    {
                        _cache.Remove(_currentKeys[_nextAvailableCacheIndex]);
                    }

                    _currentKeys[_nextAvailableCacheIndex] = key;
                    _nextAvailableCacheIndex = ++_nextAvailableCacheIndex % _currentKeys.Length;
                }

                _cache[key] = value;
            }
        }
    }
}
