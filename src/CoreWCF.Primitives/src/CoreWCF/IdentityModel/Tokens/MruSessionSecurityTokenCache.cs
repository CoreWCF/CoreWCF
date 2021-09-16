// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// An MRU cache (Most Recently Used).
    /// </summary>
    /// <remarks>
    /// Thread safe. Critsec around each method.
    /// A LinkedList is used to track MRU for fast purge.
    /// A Dictionary is used for fast keyed lookup.
    /// Grows until it reaches this.maximumSize, then purges down to this.sizeAfterPurge.
    /// </remarks>
    internal class MruSessionSecurityTokenCache : SessionSecurityTokenCache
    {
        public const int DefaultTokenCacheSize = 20000; 
        public static readonly TimeSpan DefaultPurgeInterval = TimeSpan.FromMinutes(15);
        private Dictionary<SessionSecurityTokenCacheKey, CacheEntry> _items;
        private CacheEntry _mruEntry;
        private LinkedList<SessionSecurityTokenCacheKey> _mruList;
        private int _sizeAfterPurge;
        private object _syncRoot = new object();
        
        /// <summary>
        /// Constructor to create an instance of this class.
        /// </summary>
        /// <remarks>
        /// Uses the default maximum cache size.
        /// </remarks>
        public MruSessionSecurityTokenCache()
            : this(DefaultTokenCacheSize)
        {
        }

        /// <summary>
        /// Constructor to create an instance of this class.
        /// </summary>
        /// <param name="maximumSize">Defines the maximum size of the cache.</param>
        public MruSessionSecurityTokenCache(int maximumSize)
            : this(maximumSize, null)
        {
        }

        /// <summary>
        /// Constructor to create an instance of this class.
        /// </summary>
        /// <param name="maximumSize">Defines the maximum size of the cache.</param>
        /// <param name="comparer">The method used for comparing cache entries.</param>
        public MruSessionSecurityTokenCache(int maximumSize, IEqualityComparer<SessionSecurityTokenCacheKey> comparer)
            : this((maximumSize / 5) * 4, maximumSize, comparer)
        {
        }

        /// <summary>
        /// Constructor to create an instance of this class.
        /// </summary>
        /// <param name="sizeAfterPurge">
        /// If the cache size exceeds <paramref name="maximumSize"/>, 
        /// the cache will be resized to <paramref name="sizeAfterPurge"/> by removing least recently used items.
        /// </param>
        /// <param name="maximumSize">Defines the maximum size of the cache.</param>
        public MruSessionSecurityTokenCache(int sizeAfterPurge, int maximumSize)
            : this(sizeAfterPurge, maximumSize, null)
        {
        }

        /// <summary>
        /// Constructor to create an instance of this class.
        /// </summary>
        /// <param name="sizeAfterPurge">Specifies the size to which the cache is purged after it reaches <paramref name="maximumSize"/>.</param>
        /// <param name="maximumSize">Specifies the maximum size of the cache.</param>
        /// <param name="comparer">Specifies the method used for comparing cache entries.</param>
        public MruSessionSecurityTokenCache(int sizeAfterPurge, int maximumSize, IEqualityComparer<SessionSecurityTokenCacheKey> comparer)
        {
            if (sizeAfterPurge < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.ID0008), nameof(sizeAfterPurge)));
            }

            if (sizeAfterPurge >= maximumSize)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.ID0009), nameof(sizeAfterPurge)));
            }

            // null comparer is ok
            _items = new Dictionary<SessionSecurityTokenCacheKey, CacheEntry>(maximumSize, comparer);
            MaximumSize = maximumSize;
            _mruList = new LinkedList<SessionSecurityTokenCacheKey>();
            this._sizeAfterPurge = sizeAfterPurge;
            _mruEntry = new CacheEntry();
        }

        /// <summary>
        /// Gets the maximum size of the cache
        /// </summary>
        public int MaximumSize { get; }

        /// <summary>
        /// Deletes the specified cache entry from the MruCache.
        /// </summary>
        /// <param name="key">Specifies the key for the entry to be deleted.</param>
        /// <exception cref="ArgumentNullException">The <paramref name="key"/> is null.</exception>
        public override void Remove(SessionSecurityTokenCacheKey key)
        {
            if (key == null)
            {
                return;
            }

            lock (_syncRoot)
            {
                CacheEntry entry;
                if (_items.TryGetValue(key, out entry))
                {
                    _items.Remove(key);
                    _mruList.Remove(entry.Node);
                    if (object.ReferenceEquals(_mruEntry.Node, entry.Node))
                    {
                        _mruEntry.Value = null;
                        _mruEntry.Node = null;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to add an entry to the cache or update an existing one.
        /// </summary>
        /// <param name="key">The key for the entry to be added.</param>
        /// <param name="value">The security token to be added to the cache.</param>
        /// <param name="expirationTime">The expiration time for this entry.</param>
        public override void AddOrUpdate(SessionSecurityTokenCacheKey key, SessionSecurityToken value, DateTime expirationTime)
        {
            if (key == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("key");
            }

            lock (_syncRoot)
            {
                Purge();
                Remove(key);

                // Add  the new entry to the cache and make it the MRU element
                CacheEntry entry = new CacheEntry();
                entry.Node = _mruList.AddFirst(key);
                entry.Value = value;
                _items.Add(key, entry);
                _mruEntry = entry;
            }
        }

        /// <summary>
        /// Returns the Session Security Token corresponding to the specified key exists in the cache. Also if it exists, marks it as MRU. 
        /// </summary>
        /// <param name="key">Specifies the key for the entry to be retrieved.</param>
        /// <returns>Returns the Session Security Token from the cache if found, otherwise, null.</returns>
        public override SessionSecurityToken Get(SessionSecurityTokenCacheKey key)
        {
            if (key == null)
            {
                return null;
            }

            // If found, make the entry most recently used
            SessionSecurityToken sessionToken = null;
            CacheEntry entry;
            bool found;
            
            lock (_syncRoot)
            {
                // first check our MRU item
                if (_mruEntry.Node != null && key != null && key.Equals(_mruEntry.Node.Value))
                {
                    return _mruEntry.Value;                    
                }

                found = _items.TryGetValue(key, out entry);
                if (found)
                {
                    sessionToken = entry.Value;

                    // Move the node to the head of the MRU list if it's not already there
                    if (_mruList.Count > 1 && !object.ReferenceEquals(_mruList.First, entry.Node))
                    {
                        _mruList.Remove(entry.Node);
                        _mruList.AddFirst(entry.Node);
                        _mruEntry = entry;
                    }
                }
            }

            return sessionToken;
        }

        /// <summary>
        /// Deletes matching cache entries from the MruCache.
        /// </summary>
        /// <param name="endpointId">Specifies the endpointId for the entries to be deleted.</param>
        /// <param name="contextId">Specifies the contextId for the entries to be deleted.</param>
        public override void RemoveAll(string endpointId, System.Xml.UniqueId contextId)
        {
            if (null == contextId || string.IsNullOrEmpty(endpointId))
            {
                return;
            }

            Dictionary<SessionSecurityTokenCacheKey, CacheEntry> entriesToDelete = new Dictionary<SessionSecurityTokenCacheKey, CacheEntry>();
            SessionSecurityTokenCacheKey key = new SessionSecurityTokenCacheKey(endpointId, contextId, null);
            key.IgnoreKeyGeneration = true;
            lock (_syncRoot)
            {
                foreach (SessionSecurityTokenCacheKey itemKey in _items.Keys)
                {
                    if (itemKey.Equals(key))
                    {
                        entriesToDelete.Add(itemKey, _items[itemKey]);
                    }
                }

                foreach (SessionSecurityTokenCacheKey itemKey in entriesToDelete.Keys)
                {
                    _items.Remove(itemKey);
                    CacheEntry entry = entriesToDelete[itemKey];
                    _mruList.Remove(entry.Node);
                    if (object.ReferenceEquals(_mruEntry.Node, entry.Node))
                    {
                        _mruEntry.Value = null;
                        _mruEntry.Node = null;
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to remove all entries with a matching endpoint Id from the cache.
        /// </summary>
        /// <param name="endpointId">The endpoint id for the entry to be removed.</param>
        public override void RemoveAll(string endpointId)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException(SR.Format(SR.ID4294)));
        }
        
        /// <summary>
        /// Returns all the entries that match the given key.
        /// </summary>
        /// <param name="endpointId">The endpoint id for the entries to be retrieved.</param>
        /// <param name="contextId">The context id for the entries to be retrieved.</param>
        /// <returns>A collection of all the matching entries, an empty collection of no match found.</returns>
        public override IEnumerable<SessionSecurityToken> GetAll(string endpointId, System.Xml.UniqueId contextId)
        {
            Collection<SessionSecurityToken> tokens = new Collection<SessionSecurityToken>();
            
            if (null == contextId || string.IsNullOrEmpty(endpointId))
            {
                return tokens;
            } 
            
            CacheEntry entry;
            SessionSecurityTokenCacheKey key = new SessionSecurityTokenCacheKey(endpointId, contextId, null);
            key.IgnoreKeyGeneration = true;

            lock (_syncRoot)
            {
                foreach (SessionSecurityTokenCacheKey itemKey in _items.Keys)
                {
                    if (itemKey.Equals(key))
                    {
                        entry = _items[itemKey];

                        // Move the node to the head of the MRU list if it's not already there
                        if (_mruList.Count > 1 && !object.ReferenceEquals(_mruList.First, entry.Node))
                        {
                            _mruList.Remove(entry.Node);
                            _mruList.AddFirst(entry.Node);
                            _mruEntry = entry;
                        }
                        
                        tokens.Add(entry.Value);
                    }
                }                
            }

            return tokens;
        }

        /// <summary>
        /// This method must not be called from within a read or writer lock as a deadlock will occur.
        /// Checks the time a decides if a cleanup needs to occur.
        /// </summary>
        private void Purge()
        {
            if (_items.Count >= MaximumSize)
            {
                // If the cache is full, purge enough LRU items to shrink the 
                // cache down to the low watermark
                int countToPurge = MaximumSize - _sizeAfterPurge;
                for (int i = 0; i < countToPurge; i++)
                {
                    SessionSecurityTokenCacheKey keyRemove = _mruList.Last.Value;
                    _mruList.RemoveLast();
                    _items.Remove(keyRemove);
                }

                //if (DiagnosticUtility.ShouldTrace(TraceEventType.Information))
                //{
                //    TraceUtility.TraceString(
                //        TraceEventType.Information,
                //        SR.Format(
                //        SR.ID8003,
                //        this.maximumSize,
                //        this.sizeAfterPurge));
                //}
            }
        }
       
        public class CacheEntry
        {
            public SessionSecurityToken Value
            { 
                get; 
                set; 
            }

            public LinkedListNode<SessionSecurityTokenCacheKey> Node
            { 
                get; 
                set;
            }
        }
    }
}
