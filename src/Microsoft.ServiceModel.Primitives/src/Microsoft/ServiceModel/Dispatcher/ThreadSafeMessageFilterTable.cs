using System.Collections;
using System.Collections.Generic;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal class ThreadSafeMessageFilterTable<FilterData> : IMessageFilterTable<FilterData>
    {
        MessageFilterTable<FilterData> table;
        object syncRoot;

        internal ThreadSafeMessageFilterTable()
        {
            table = new MessageFilterTable<FilterData>();
            syncRoot = new object();
        }

        internal object SyncRoot
        {
            get { return syncRoot; }
        }

        public int DefaultPriority
        {
            get
            {
                lock (syncRoot)
                {
                    return table.DefaultPriority;
                }
            }
            set
            {
                lock (syncRoot)
                {
                    table.DefaultPriority = value;
                }
            }
        }

        internal void Add(MessageFilter filter, FilterData data, int priority)
        {
            lock (syncRoot)
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
                lock (syncRoot)
                {
                    return table.Count;
                }
            }
        }

        public void Clear()
        {
            lock (syncRoot)
            {
                table.Clear();
            }
        }

        public bool GetMatchingValue(Message message, out FilterData data)
        {
            lock (syncRoot)
            {
                return table.GetMatchingValue(message, out data);
            }
        }

        public bool GetMatchingValue(MessageBuffer buffer, out FilterData data)
        {
            lock (syncRoot)
            {
                return table.GetMatchingValue(buffer, out data);
            }
        }

        public bool GetMatchingValues(Message message, ICollection<FilterData> results)
        {
            lock (syncRoot)
            {
                return table.GetMatchingValues(message, results);
            }
        }

        public bool GetMatchingValues(MessageBuffer buffer, ICollection<FilterData> results)
        {
            lock (syncRoot)
            {
                return table.GetMatchingValues(buffer, results);
            }
        }

        public bool GetMatchingFilter(Message message, out MessageFilter filter)
        {
            lock (syncRoot)
            {
                return table.GetMatchingFilter(message, out filter);
            }
        }

        public bool GetMatchingFilter(MessageBuffer buffer, out MessageFilter filter)
        {
            lock (syncRoot)
            {
                return table.GetMatchingFilter(buffer, out filter);
            }
        }

        public bool GetMatchingFilters(Message message, ICollection<MessageFilter> results)
        {
            lock (syncRoot)
            {
                return table.GetMatchingFilters(message, results);
            }
        }

        public bool GetMatchingFilters(MessageBuffer buffer, ICollection<MessageFilter> results)
        {
            lock (syncRoot)
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
                lock (syncRoot)
                {
                    return table[key];
                }
            }
            set
            {
                lock (syncRoot)
                {
                    table[key] = value;
                }
            }
        }

        public ICollection<MessageFilter> Keys
        {
            get
            {
                lock (syncRoot)
                {
                    return table.Keys;
                }
            }
        }

        public ICollection<FilterData> Values
        {
            get
            {
                lock (syncRoot)
                {
                    return table.Values;
                }
            }
        }

        public bool ContainsKey(MessageFilter key)
        {
            lock (syncRoot)
            {
                return table.ContainsKey(key);
            }
        }

        public void Add(MessageFilter key, FilterData value)
        {
            lock (syncRoot)
            {
                table.Add(key, value);
            }
        }

        public bool Remove(MessageFilter key)
        {
            lock (syncRoot)
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
                lock (syncRoot)
                {
                    return ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).IsReadOnly;
                }
            }
        }

        void ICollection<KeyValuePair<MessageFilter, FilterData>>.Add(KeyValuePair<MessageFilter, FilterData> item)
        {
            lock (syncRoot)
            {
                ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).Add(item);
            }
        }

        bool ICollection<KeyValuePair<MessageFilter, FilterData>>.Contains(KeyValuePair<MessageFilter, FilterData> item)
        {
            lock (syncRoot)
            {
                return ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).Contains(item);
            }
        }

        void ICollection<KeyValuePair<MessageFilter, FilterData>>.CopyTo(KeyValuePair<MessageFilter, FilterData>[] array, int arrayIndex)
        {
            lock (syncRoot)
            {
                ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).CopyTo(array, arrayIndex);
            }
        }

        bool ICollection<KeyValuePair<MessageFilter, FilterData>>.Remove(KeyValuePair<MessageFilter, FilterData> item)
        {
            lock (syncRoot)
            {
                return ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).Remove(item);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (syncRoot)
            {
                return ((IEnumerable<KeyValuePair<MessageFilter, FilterData>>)this).GetEnumerator();
            }
        }

        IEnumerator<KeyValuePair<MessageFilter, FilterData>> IEnumerable<KeyValuePair<MessageFilter, FilterData>>.GetEnumerator()
        {
            lock (syncRoot)
            {
                return ((ICollection<KeyValuePair<MessageFilter, FilterData>>)table).GetEnumerator();
            }
        }

        public bool TryGetValue(MessageFilter filter, out FilterData data)
        {
            lock (syncRoot)
            {
                return table.TryGetValue(filter, out data);
            }
        }
    }
}