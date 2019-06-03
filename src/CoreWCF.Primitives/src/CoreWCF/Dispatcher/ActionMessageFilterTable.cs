using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;

namespace CoreWCF.Dispatcher
{
    [DataContract]
    internal class ActionMessageFilterTable<TFilterData> : IMessageFilterTable<TFilterData>
    {
        Dictionary<MessageFilter, TFilterData> filters;
        Dictionary<string, List<MessageFilter>> actions;
        List<MessageFilter> always;

        public ActionMessageFilterTable()
        {
            Init();
        }

        void Init()
        {
            filters = new Dictionary<MessageFilter, TFilterData>();
            actions = new Dictionary<string, List<MessageFilter>>();
            always = new List<MessageFilter>();
        }

        public TFilterData this[MessageFilter filter]
        {
            get
            {
                return filters[filter];
            }
            set
            {
                if (filters.ContainsKey(filter))
                {
                    filters[filter] = value;
                }
                else
                {
                    Add(filter, value);
                }
            }
        }

        public int Count
        {
            get
            {
                return filters.Count;
            }
        }

        [DataMember(IsRequired = true)]
        Entry[] Entries
        {
            get
            {
                Entry[] entries = new Entry[Count];
                int i = 0;
                foreach (KeyValuePair<MessageFilter, TFilterData> item in filters)
                    entries[i++] = new Entry(item.Key, item.Value);

                return entries;
            }
            set
            {
                Init();

                for (int i = 0; i < value.Length; ++i)
                    Add(value[i].filter, value[i].data);
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public ICollection<MessageFilter> Keys
        {
            get
            {
                return filters.Keys;
            }
        }

        public ICollection<TFilterData> Values
        {
            get
            {
                return filters.Values;
            }
        }

        public void Add(ActionMessageFilter filter, TFilterData data)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter");
            }

            this.filters.Add(filter, data);
            List<MessageFilter> filters;
            if (filter.Actions.Count == 0)
            {
                always.Add(filter);
            }
            else
            {
                for (int i = 0; i < filter.Actions.Count; ++i)
                {
                    if (!actions.TryGetValue(filter.Actions[i], out filters))
                    {
                        filters = new List<MessageFilter>();
                        actions.Add(filter.Actions[i], filters);
                    }
                    filters.Add(filter);
                }
            }
        }

        public void Add(MessageFilter filter, TFilterData data)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter");
            }

            Add((ActionMessageFilter)filter, data);
        }

        public void Add(KeyValuePair<MessageFilter, TFilterData> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            filters.Clear();
            actions.Clear();
            always.Clear();
        }

        public bool Contains(KeyValuePair<MessageFilter, TFilterData> item)
        {
            return ((ICollection<KeyValuePair<MessageFilter, TFilterData>>)filters).Contains(item);
        }

        public bool ContainsKey(MessageFilter filter)
        {
            return filters.ContainsKey(filter);
        }

        public void CopyTo(KeyValuePair<MessageFilter, TFilterData>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<MessageFilter, TFilterData>>)filters).CopyTo(array, arrayIndex);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<MessageFilter, TFilterData>> GetEnumerator()
        {
            return ((ICollection<KeyValuePair<MessageFilter, TFilterData>>)filters).GetEnumerator();
        }

        MessageFilter InnerMatch(Message message)
        {
            string act = message.Headers.Action;
            if (act == null)
            {
                act = string.Empty;
            }

            List<MessageFilter> filters;
            if (actions.TryGetValue(act, out filters))
            {
                if (always.Count + filters.Count > 1)
                {
                    List<MessageFilter> tmp = new List<MessageFilter>(filters);
                    tmp.AddRange(always);
                    Collection<MessageFilter> matches = new Collection<MessageFilter>(tmp);
                    throw TraceUtility.ThrowHelperError(new MultipleFilterMatchesException(SR.FilterMultipleMatches, null, matches), message);
                }
                return filters[0];
            }

            if (always.Count > 1)
            {
                Collection<MessageFilter> matches = new Collection<MessageFilter>(new List<MessageFilter>(always));
                throw TraceUtility.ThrowHelperError(new MultipleFilterMatchesException(SR.FilterMultipleMatches, null, matches), message);
            }
            else if (always.Count == 1)
            {
                return always[0];
            }

            return null;
        }

        void InnerMatch(Message message, ICollection<MessageFilter> results)
        {
            for (int i = 0; i < always.Count; ++i)
            {
                results.Add(always[i]);
            }

            string act = message.Headers.Action;
            if (act == null)
            {
                act = string.Empty;
            }

            List<MessageFilter> filters;
            if (actions.TryGetValue(act, out filters))
            {
                for (int i = 0; i < filters.Count; ++i)
                {
                    results.Add(filters[i]);
                }
            }
        }

        void InnerMatchData(Message message, ICollection<TFilterData> results)
        {
            for (int i = 0; i < always.Count; ++i)
            {
                results.Add(this.filters[always[i]]);
            }

            string act = message.Headers.Action;
            if (act == null)
            {
                act = string.Empty;
            }

            List<MessageFilter> filters;
            if (actions.TryGetValue(act, out filters))
            {
                for (int i = 0; i < filters.Count; ++i)
                {
                    results.Add(this.filters[filters[i]]);
                }
            }
        }

        public bool GetMatchingValue(Message message, out TFilterData data)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }

            MessageFilter f = InnerMatch(message);
            if (f == null)
            {
                data = default(TFilterData);
                return false;
            }

            data = filters[f];
            return true;
        }

        public bool GetMatchingValue(MessageBuffer messageBuffer, out TFilterData data)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("messageBuffer");
            }

            MessageFilter f = null;
            Message msg = messageBuffer.CreateMessage();
            try
            {
                f = InnerMatch(msg);
            }
            finally
            {
                msg.Close();
            }

            if (f == null)
            {
                data = default(TFilterData);
                return false;
            }

            data = filters[f];
            return true;
        }

        public bool GetMatchingFilter(Message message, out MessageFilter filter)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }

            filter = InnerMatch(message);
            return filter != null;
        }

        public bool GetMatchingFilter(MessageBuffer messageBuffer, out MessageFilter filter)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("messageBuffer");
            }

            Message msg = messageBuffer.CreateMessage();
            try
            {
                filter = InnerMatch(msg);
                return filter != null;
            }
            finally
            {
                msg.Close();
            }
        }

        public bool GetMatchingFilters(Message message, ICollection<MessageFilter> results)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }

            if (results == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("results");
            }

            int count = results.Count;
            InnerMatch(message, results);
            return count != results.Count;
        }

        public bool GetMatchingFilters(MessageBuffer messageBuffer, ICollection<MessageFilter> results)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("messageBuffer");
            }

            if (results == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("results");
            }

            Message msg = messageBuffer.CreateMessage();
            try
            {
                int count = results.Count;
                InnerMatch(msg, results);
                return count != results.Count;
            }
            finally
            {
                msg.Close();
            }
        }

        public bool GetMatchingValues(Message message, ICollection<TFilterData> results)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("message");
            }

            if (results == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("results");
            }

            int count = results.Count;
            InnerMatchData(message, results);
            return count != results.Count;
        }

        public bool GetMatchingValues(MessageBuffer messageBuffer, ICollection<TFilterData> results)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("messageBuffer");
            }

            if (results == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("results");
            }

            Message msg = messageBuffer.CreateMessage();
            try
            {
                int count = results.Count;
                InnerMatchData(msg, results);
                return count != results.Count;
            }
            finally
            {
                msg.Close();
            }
        }

        public bool Remove(ActionMessageFilter filter)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter");
            }

            if (this.filters.Remove(filter))
            {
                if (filter.Actions.Count == 0)
                {
                    always.Remove(filter);
                }
                else
                {
                    List<MessageFilter> filters;
                    for (int i = 0; i < filter.Actions.Count; ++i)
                    {
                        filters = actions[filter.Actions[i]];
                        if (filters.Count == 1)
                        {
                            actions.Remove(filter.Actions[i]);
                        }
                        else
                        {
                            filters.Remove(filter);
                        }
                    }
                }
                return true;
            }
            return false;
        }

        public bool Remove(MessageFilter filter)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter");
            }

            ActionMessageFilter aFilter = filter as ActionMessageFilter;
            if (aFilter != null)
            {
                return Remove(aFilter);
            }
            return false;
        }

        public bool Remove(KeyValuePair<MessageFilter, TFilterData> item)
        {
            if (((ICollection<KeyValuePair<MessageFilter, TFilterData>>)filters).Contains(item))
            {
                return Remove(item.Key);
            }
            return false;
        }

        public bool TryGetValue(MessageFilter filter, out TFilterData data)
        {
            return filters.TryGetValue(filter, out data);
        }

        [DataContract]
        class Entry
        {
            [DataMember(IsRequired = true)]
            internal MessageFilter filter;

            [DataMember(IsRequired = true)]
            internal TFilterData data;

            internal Entry(MessageFilter f, TFilterData d)
            {
                filter = f;
                data = d;
            }
        }
    }

}