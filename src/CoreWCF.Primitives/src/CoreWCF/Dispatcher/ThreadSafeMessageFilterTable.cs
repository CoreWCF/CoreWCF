// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal class ThreadSafeMessageFilterTable<FilterData> : IMessageFilterTable<FilterData>
    {
        private readonly MessageFilterTable<FilterData> table;

        internal ThreadSafeMessageFilterTable()
        {
            table = new MessageFilterTable<FilterData>();
            SyncRoot = new object();
        }

        internal object SyncRoot { get; }

        public int DefaultPriority
        {
            get
            {
                lock (SyncRoot)
                {
                    return table.DefaultPriority;
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    table.DefaultPriority = value;
                }
            }
        }

        internal void Add(MessageFilter filter, FilterData data, int priority)
        {
            lock (SyncRoot)
            {
                table.Add(filter, data, priority);
            }
        }

        //
        // IMessageFilterTable<FilterData> methods
        //

        public int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return table.Count;
                }
            }
        }

        public void Clear()
        {
            lock (SyncRoot)
            {
                table.Clear();
            }
        }

        public bool GetMatchingValue(Message message, out FilterData data)
        {
            lock (SyncRoot)
            {
                return table.GetMatchingValue(message, out data);
            }
        }

        public bool GetMatchingValue(MessageBuffer buffer, out FilterData data)
        {
            lock (SyncRoot)
            {
                return table.GetMatchingValue(buffer, out data);
            }
        }

        public bool GetMatchingValues(Message message, ICollection<FilterData> results)
        {
            lock (SyncRoot)
            {
                return table.GetMatchingValues(message, results);
            }
        }

        public bool GetMatchingValues(MessageBuffer buffer, ICollection<FilterData> results)
        {
            lock (SyncRoot)
            {
                return table.GetMatchingValues(buffer, results);
            }
        }

        public bool GetMatchingFilter(Message message, out MessageFilter filter)
        {
            lock (SyncRoot)
            {
                return table.GetMatchingFilter(message, out filter);
            }
        }

        public bool GetMatchingFilter(MessageBuffer buffer, out MessageFilter filter)
        {
            lock (SyncRoot)
            {
                return table.GetMatchingFilter(buffer, out filter);
            }
        }

        public bool GetMatchingFilters(Message message, ICollection<MessageFilter> results)
        {
            lock (SyncRoot)
            {
                return table.GetMatchingFilters(message, results);
            }
        }

        public bool GetMatchingFilters(MessageBuffer buffer, ICollection<MessageFilter> results)
        {
            lock (SyncRoot)
            {
                return table.GetMatchingFilters(buffer, results);
            }
        }

        //
        // IDictionary<MessageFilter,FilterData> methods
        //   

        public FilterData this[MessageFilter key]
        {
            get
            {
                lock (SyncRoot)
                {
                    return table[key];
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    table[key] = value;
                }
            }
        }

        public ICollection<MessageFilter> Keys
        {
            get
            {
                lock (SyncRoot)
                {
                    return table.Keys;
                }
            }
        }

        public ICollection<FilterData> Values
        {
            get
            {
                lock (SyncRoot)
                {
                    return table.Values;
                }
            }
        }

        public bool ContainsKey(MessageFilter key)
        {
            lock (SyncRoot)
            {
                return table.ContainsKey(key);
            }
        }

        public void Add(MessageFilter key, FilterData value)
        {
            lock (SyncRoot)
            {
                table.Add(key, value);
            }
        }

        public bool Remove(MessageFilter key)
        {
            lock (SyncRoot)
            {
                return table.Remove(key);
            }
        }

        //
        // ICollection<KeyValuePair<MessageFilter,FilterData>> methods
        //

        bool ICollection<KeyValuePair<MessageFilter, FilterData>>.IsReadOnly
        {
            get
            {
                lock (SyncRoot)
                {
                    return ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).IsReadOnly;
                }
            }
        }

        void ICollection<KeyValuePair<MessageFilter, FilterData>>.Add(KeyValuePair<MessageFilter, FilterData> item)
        {
            lock (SyncRoot)
            {
                ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).Add(item);
            }
        }

        bool ICollection<KeyValuePair<MessageFilter, FilterData>>.Contains(KeyValuePair<MessageFilter, FilterData> item)
        {
            lock (SyncRoot)
            {
                return ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).Contains(item);
            }
        }

        void ICollection<KeyValuePair<MessageFilter, FilterData>>.CopyTo(KeyValuePair<MessageFilter, FilterData>[] array, int arrayIndex)
        {
            lock (SyncRoot)
            {
                ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).CopyTo(array, arrayIndex);
            }
        }

        bool ICollection<KeyValuePair<MessageFilter, FilterData>>.Remove(KeyValuePair<MessageFilter, FilterData> item)
        {
            lock (SyncRoot)
            {
                return ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).Remove(item);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (SyncRoot)
            {
                return ((IEnumerable<KeyValuePair<MessageFilter, FilterData>>)this).GetEnumerator();
            }
        }

        IEnumerator<KeyValuePair<MessageFilter, FilterData>> IEnumerable<KeyValuePair<MessageFilter, FilterData>>.GetEnumerator()
        {
            lock (SyncRoot)
            {
                return ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).GetEnumerator();
            }
        }

        public bool TryGetValue(MessageFilter filter, out FilterData data)
        {
            lock (SyncRoot)
            {
                return table.TryGetValue(filter, out data);
            }
        }
    }
}