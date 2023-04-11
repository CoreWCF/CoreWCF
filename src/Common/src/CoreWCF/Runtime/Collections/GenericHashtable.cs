// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CoreWCF.Runtime.Collections
{
    internal class GenericHashtable<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private Hashtable _hashtable;

        public GenericHashtable()
        {
            _hashtable = Hashtable.Synchronized(new Hashtable());
        }

        public TValue this[TKey key]
        {
            get
            {
                if (key == null) throw new ArgumentNullException(nameof(key));
                if (!_hashtable.ContainsKey(key))
                {
                    throw new KeyNotFoundException();
                }

                return (TValue)_hashtable[key];
            }
            set { _hashtable[key] = value; }
        }

        public ICollection<TKey> Keys => _hashtable.Keys.Cast<TKey>().ToList();
        public ICollection<TValue> Values => _hashtable.Values.Cast<TValue>().ToList();
        public int Count => _hashtable.Count;
        public bool IsReadOnly => _hashtable.IsReadOnly;
        public void Add(TKey key, TValue value) => _hashtable.Add(key, value);
        public void Add(KeyValuePair<TKey, TValue> item) => _hashtable.Add(item.Key, item.Value);
        public void Clear() => _hashtable.Clear();

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            // Two reads from table need to be treated atomic otherwise Contains can return true
            // and this this[item.Key] might not find the key if a remove was in progress.
            lock (_hashtable.SyncRoot)
            {
                return _hashtable.Contains(item.Key) && this[item.Key].Equals(item.Value);
            }
        }

        public bool ContainsKey(TKey key) => _hashtable.ContainsKey(key);

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            if (array == null) throw new ArgumentNullException(nameof(array));
            if ((uint)arrayIndex > (uint)array.Length || array.Length - arrayIndex < Count)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            foreach (DictionaryEntry entry in _hashtable)
            {
                var keyValuePair = new KeyValuePair<TKey, TValue>((TKey)entry.Key, (TValue)entry.Value);
                array[arrayIndex++] = keyValuePair;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            foreach (DictionaryEntry entry in _hashtable)
            {
                yield return new KeyValuePair<TKey, TValue>((TKey)entry.Key, (TValue)entry.Value);
            }
        }

        public bool Remove(TKey key)
        {
            lock (_hashtable.SyncRoot)
            {
                if (!ContainsKey(key))
                    return false;

                _hashtable.Remove(key);
                return true;
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            // Need to wrap in lock otherwise the value could change between call to Contains(item) and
            // the remove call
            lock (_hashtable.SyncRoot)
            {
                if (!Contains(item))
                    return false;

                _hashtable.Remove(item.Key);
                return true;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            // Lock free optimization when TValue is a value type
            if (typeof(TValue).IsValueType)
            {
                object obj = _hashtable[key];
                // Can only be null if not found, otherwise if found will always be a boxed value type
                if (obj != null)
                {
                    value = (TValue)obj;
                    return true;
                }

                value = default;
                return false;
            }

            // When TValue is nullable, it might be in the Hashtable with a value of null so need to do
            // 2 lookups, one to see if it's there, another to get the (possibly null) value. Needs to
            // be atomic otherwise table could get modified between the 2 reads so using lock.
            lock (_hashtable.SyncRoot)
            {
                // Need to call ContainsKey as value might be null so getting value and null checking
                // is insufficient.
                if (!_hashtable.ContainsKey(key))
                {
                    value = default;
                    return false;
                }

                value = this[key];
                return true;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}