// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
        private Dictionary<MessageFilter, TFilterData> _filters;
        private Dictionary<string, List<MessageFilter>> _actions;
        private List<MessageFilter> _always;

        public ActionMessageFilterTable()
        {
            Init();
        }

        private void Init()
        {
            _filters = new Dictionary<MessageFilter, TFilterData>();
            _actions = new Dictionary<string, List<MessageFilter>>();
            _always = new List<MessageFilter>();
        }

        public TFilterData this[MessageFilter filter]
        {
            get
            {
                return _filters[filter];
            }
            set
            {
                if (_filters.ContainsKey(filter))
                {
                    _filters[filter] = value;
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
                return _filters.Count;
            }
        }

        [DataMember(IsRequired = true)]
        private Entry[] Entries
        {
            get
            {
                Entry[] entries = new Entry[Count];
                int i = 0;
                foreach (KeyValuePair<MessageFilter, TFilterData> item in _filters)
                {
                    entries[i++] = new Entry(item.Key, item.Value);
                }

                return entries;
            }
            set
            {
                Init();

                for (int i = 0; i < value.Length; ++i)
                {
                    Add(value[i].filter, value[i].data);
                }
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
                return _filters.Keys;
            }
        }

        public ICollection<TFilterData> Values
        {
            get
            {
                return _filters.Values;
            }
        }

        public void Add(ActionMessageFilter filter, TFilterData data)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            _filters.Add(filter, data);
            if (filter.Actions.Count == 0)
            {
                _always.Add(filter);
            }
            else
            {
                for (int i = 0; i < filter.Actions.Count; ++i)
                {
                    if (!_actions.TryGetValue(filter.Actions[i], out List<MessageFilter> filters))
                    {
                        filters = new List<MessageFilter>();
                        _actions.Add(filter.Actions[i], filters);
                    }
                    filters.Add(filter);
                }
            }
        }

        public void Add(MessageFilter filter, TFilterData data)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            Add((ActionMessageFilter)filter, data);
        }

        public void Add(KeyValuePair<MessageFilter, TFilterData> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            _filters.Clear();
            _actions.Clear();
            _always.Clear();
        }

        public bool Contains(KeyValuePair<MessageFilter, TFilterData> item)
        {
            return ((ICollection<KeyValuePair<MessageFilter, TFilterData>>)_filters).Contains(item);
        }

        public bool ContainsKey(MessageFilter filter)
        {
            return _filters.ContainsKey(filter);
        }

        public void CopyTo(KeyValuePair<MessageFilter, TFilterData>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<MessageFilter, TFilterData>>)_filters).CopyTo(array, arrayIndex);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<MessageFilter, TFilterData>> GetEnumerator()
        {
            return ((ICollection<KeyValuePair<MessageFilter, TFilterData>>)_filters).GetEnumerator();
        }

        private MessageFilter InnerMatch(Message message)
        {
            string act = message.Headers.Action;
            if (act == null)
            {
                act = string.Empty;
            }

            if (_actions.TryGetValue(act, out List<MessageFilter> filters))
            {
                if (_always.Count + filters.Count > 1)
                {
                    List<MessageFilter> tmp = new List<MessageFilter>(filters);
                    tmp.AddRange(_always);
                    Collection<MessageFilter> matches = new Collection<MessageFilter>(tmp);
                    throw TraceUtility.ThrowHelperError(new MultipleFilterMatchesException(SR.FilterMultipleMatches, null, matches), message);
                }
                return filters[0];
            }

            if (_always.Count > 1)
            {
                Collection<MessageFilter> matches = new Collection<MessageFilter>(new List<MessageFilter>(_always));
                throw TraceUtility.ThrowHelperError(new MultipleFilterMatchesException(SR.FilterMultipleMatches, null, matches), message);
            }
            else if (_always.Count == 1)
            {
                return _always[0];
            }

            return null;
        }

        private void InnerMatch(Message message, ICollection<MessageFilter> results)
        {
            for (int i = 0; i < _always.Count; ++i)
            {
                results.Add(_always[i]);
            }

            string act = message.Headers.Action;
            if (act == null)
            {
                act = string.Empty;
            }

            if (_actions.TryGetValue(act, out List<MessageFilter> filters))
            {
                for (int i = 0; i < filters.Count; ++i)
                {
                    results.Add(filters[i]);
                }
            }
        }

        private void InnerMatchData(Message message, ICollection<TFilterData> results)
        {
            for (int i = 0; i < _always.Count; ++i)
            {
                results.Add(_filters[_always[i]]);
            }

            string act = message.Headers.Action;
            if (act == null)
            {
                act = string.Empty;
            }

            if (_actions.TryGetValue(act, out List<MessageFilter> filters))
            {
                for (int i = 0; i < filters.Count; ++i)
                {
                    results.Add(_filters[filters[i]]);
                }
            }
        }

        public bool GetMatchingValue(Message message, out TFilterData data)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            MessageFilter f = InnerMatch(message);
            if (f == null)
            {
                data = default;
                return false;
            }

            data = _filters[f];
            return true;
        }

        public bool GetMatchingValue(MessageBuffer messageBuffer, out TFilterData data)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
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
                data = default;
                return false;
            }

            data = _filters[f];
            return true;
        }

        public bool GetMatchingFilter(Message message, out MessageFilter filter)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            filter = InnerMatch(message);
            return filter != null;
        }

        public bool GetMatchingFilter(MessageBuffer messageBuffer, out MessageFilter filter)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            if (results == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(results));
            }

            int count = results.Count;
            InnerMatch(message, results);
            return count != results.Count;
        }

        public bool GetMatchingFilters(MessageBuffer messageBuffer, ICollection<MessageFilter> results)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
            }

            if (results == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(results));
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            if (results == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(results));
            }

            int count = results.Count;
            InnerMatchData(message, results);
            return count != results.Count;
        }

        public bool GetMatchingValues(MessageBuffer messageBuffer, ICollection<TFilterData> results)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
            }

            if (results == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(results));
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            if (_filters.Remove(filter))
            {
                if (filter.Actions.Count == 0)
                {
                    _always.Remove(filter);
                }
                else
                {
                    List<MessageFilter> filters;
                    for (int i = 0; i < filter.Actions.Count; ++i)
                    {
                        filters = _actions[filter.Actions[i]];
                        if (filters.Count == 1)
                        {
                            _actions.Remove(filter.Actions[i]);
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            if (filter is ActionMessageFilter aFilter)
            {
                return Remove(aFilter);
            }
            return false;
        }

        public bool Remove(KeyValuePair<MessageFilter, TFilterData> item)
        {
            if (((ICollection<KeyValuePair<MessageFilter, TFilterData>>)_filters).Contains(item))
            {
                return Remove(item.Key);
            }
            return false;
        }

        public bool TryGetValue(MessageFilter filter, out TFilterData data)
        {
            return _filters.TryGetValue(filter, out data);
        }

        [DataContract]
        private class Entry
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