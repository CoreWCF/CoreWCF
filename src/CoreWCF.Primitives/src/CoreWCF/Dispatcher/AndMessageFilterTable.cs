// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;

namespace CoreWCF.Dispatcher
{
    internal class AndMessageFilterTable<FilterData> : IMessageFilterTable<FilterData>
    {
        private readonly Dictionary<MessageFilter, FilterData> _filters;
        private readonly Dictionary<MessageFilter, FilterDataPair> _filterData;
        private readonly MessageFilterTable<FilterDataPair> _table;

        public AndMessageFilterTable()
        {
            _filters = new Dictionary<MessageFilter, FilterData>();
            _filterData = new Dictionary<MessageFilter, FilterDataPair>();
            _table = new MessageFilterTable<FilterDataPair>();
        }

        public FilterData this[MessageFilter filter]
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
                    _filterData[filter].data = value;
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

        public ICollection<FilterData> Values
        {
            get
            {
                return _filters.Values;
            }
        }

        public void Add(MessageFilter filter, FilterData data)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            Add((AndMessageFilter)filter, data);
        }

        public void Add(KeyValuePair<MessageFilter, FilterData> item)
        {
            Add(item.Key, item.Value);
        }
        public void Add(AndMessageFilter filter, FilterData data)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            _filters.Add(filter, data);

            FilterDataPair pair = new FilterDataPair(filter, data);
            _filterData.Add(filter, pair);

            _table.Add(filter.Filter1, pair);
        }

        public void Clear()
        {
            _filters.Clear();
            _filterData.Clear();
            _table.Clear();
        }

        public bool Contains(KeyValuePair<MessageFilter, FilterData> item)
        {
            return ((ICollection<KeyValuePair<MessageFilter, FilterData>>)_filters).Contains(item);
        }

        public bool ContainsKey(MessageFilter filter)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }
            return _filters.ContainsKey(filter);
        }

        public void CopyTo(KeyValuePair<MessageFilter, FilterData>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<MessageFilter, FilterData>>)_filters).CopyTo(array, arrayIndex);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<MessageFilter, FilterData>> GetEnumerator()
        {
            return _filters.GetEnumerator();
        }

        private FilterDataPair InnerMatch(Message message)
        {
            List<FilterDataPair> pairs = new List<FilterDataPair>();
            _table.GetMatchingValues(message, pairs);

            FilterDataPair pair = null;
            for (int i = 0; i < pairs.Count; ++i)
            {
                if (pairs[i].filter.Filter2.Match(message))
                {
                    if (pair != null)
                    {
                        Collection<MessageFilter> matches = new Collection<MessageFilter>
                        {
                            pair.filter,
                            pairs[i].filter
                        };
                        throw TraceUtility.ThrowHelperError(new MultipleFilterMatchesException(SR.FilterMultipleMatches, null, matches), message);
                    }
                    pair = pairs[i];
                }
            }

            return pair;
        }

        private FilterDataPair InnerMatch(MessageBuffer messageBuffer)
        {
            List<FilterDataPair> pairs = new List<FilterDataPair>();
            _table.GetMatchingValues(messageBuffer, pairs);

            FilterDataPair pair = null;
            for (int i = 0; i < pairs.Count; ++i)
            {
                if (pairs[i].filter.Filter2.Match(messageBuffer))
                {
                    if (pair != null)
                    {
                        Collection<MessageFilter> matches = new Collection<MessageFilter>
                        {
                            pair.filter,
                            pairs[i].filter
                        };
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MultipleFilterMatchesException(SR.FilterMultipleMatches, null, matches));
                    }
                    pair = pairs[i];
                }
            }

            return pair;
        }

        private void InnerMatch(Message message, ICollection<MessageFilter> results)
        {
            List<FilterDataPair> pairs = new List<FilterDataPair>();
            _table.GetMatchingValues(message, pairs);

            for (int i = 0; i < pairs.Count; ++i)
            {
                if (pairs[i].filter.Filter2.Match(message))
                {
                    results.Add(pairs[i].filter);
                }
            }
        }

        private void InnerMatchData(Message message, ICollection<FilterData> results)
        {
            List<FilterDataPair> pairs = new List<FilterDataPair>();
            _table.GetMatchingValues(message, pairs);

            for (int i = 0; i < pairs.Count; ++i)
            {
                if (pairs[i].filter.Filter2.Match(message))
                {
                    results.Add(pairs[i].data);
                }
            }
        }

        private void InnerMatch(MessageBuffer messageBuffer, ICollection<MessageFilter> results)
        {
            List<FilterDataPair> pairs = new List<FilterDataPair>();
            _table.GetMatchingValues(messageBuffer, pairs);

            for (int i = 0; i < pairs.Count; ++i)
            {
                if (pairs[i].filter.Filter2.Match(messageBuffer))
                {
                    results.Add(pairs[i].filter);
                }
            }
        }

        private void InnerMatchData(MessageBuffer messageBuffer, ICollection<FilterData> results)
        {
            List<FilterDataPair> pairs = new List<FilterDataPair>();
            _table.GetMatchingValues(messageBuffer, pairs);

            for (int i = 0; i < pairs.Count; ++i)
            {
                if (pairs[i].filter.Filter2.Match(messageBuffer))
                {
                    results.Add(pairs[i].data);
                }
            }
        }

        internal bool GetMatchingValue(Message message, out FilterData data, out bool addressMatched)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            List<FilterDataPair> pairs = new List<FilterDataPair>();
            addressMatched = _table.GetMatchingValues(message, pairs);

            FilterDataPair pair = null;
            for (int i = 0; i < pairs.Count; ++i)
            {
                if (pairs[i].filter.Filter2.Match(message))
                {
                    if (pair != null)
                    {
                        Collection<MessageFilter> matches = new Collection<MessageFilter>
                        {
                            pair.filter,
                            pairs[i].filter
                        };
                        throw TraceUtility.ThrowHelperError(new MultipleFilterMatchesException(SR.FilterMultipleMatches, null, matches), message);
                    }
                    pair = pairs[i];
                }
            }

            if (pair == null)
            {
                data = default;
                return false;
            }

            data = pair.data;
            return true;
        }

        public bool GetMatchingValue(Message message, out FilterData data)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            FilterDataPair pair = InnerMatch(message);
            if (pair == null)
            {
                data = default;
                return false;
            }

            data = pair.data;
            return true;
        }

        public bool GetMatchingValue(MessageBuffer messageBuffer, out FilterData data)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
            }

            FilterDataPair pair = InnerMatch(messageBuffer);

            if (pair == null)
            {
                data = default;
                return false;
            }

            data = pair.data;
            return true;
        }

        public bool GetMatchingFilter(Message message, out MessageFilter filter)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            FilterDataPair pair = InnerMatch(message);
            if (pair == null)
            {
                filter = null;
                return false;
            }

            filter = pair.filter;
            return true;
        }

        public bool GetMatchingFilter(MessageBuffer messageBuffer, out MessageFilter filter)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
            }

            FilterDataPair pair = InnerMatch(messageBuffer);

            if (pair == null)
            {
                filter = null;
                return false;
            }

            filter = pair.filter;
            return true;
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

            int count = results.Count;
            InnerMatch(messageBuffer, results);
            return count != results.Count;
        }

        public bool GetMatchingValues(Message message, ICollection<FilterData> results)
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

        public bool GetMatchingValues(MessageBuffer messageBuffer, ICollection<FilterData> results)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
            }

            if (results == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(results));
            }

            int count = results.Count;
            InnerMatchData(messageBuffer, results);
            return count != results.Count;
        }

        public bool Remove(MessageFilter filter)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            if (filter is AndMessageFilter sbFilter)
            {
                return Remove(sbFilter);
            }
            return false;
        }

        public bool Remove(KeyValuePair<MessageFilter, FilterData> item)
        {
            if (((ICollection<KeyValuePair<MessageFilter, FilterData>>)_filters).Contains(item))
            {
                return Remove(item.Key);
            }
            return false;
        }

        public bool Remove(AndMessageFilter filter)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            if (_filters.Remove(filter))
            {
                _filterData.Remove(filter);
                _table.Remove(filter.Filter1);

                return true;
            }
            return false;
        }

        internal class FilterDataPair
        {
            internal AndMessageFilter filter;
            internal FilterData data;

            internal FilterDataPair(AndMessageFilter filter, FilterData data)
            {
                this.filter = filter;
                this.data = data;
            }
        }

        public bool TryGetValue(MessageFilter filter, out FilterData data)
        {
            return _filters.TryGetValue(filter, out data);
        }
    }
}