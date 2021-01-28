// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;

namespace CoreWCF.Dispatcher
{
    using HeaderBit = CoreWCF.Dispatcher.EndpointAddressProcessor.HeaderBit;
    using QName = CoreWCF.Dispatcher.EndpointAddressProcessor.QName;

    internal class EndpointAddressMessageFilterTable<TFilterData> : IMessageFilterTable<TFilterData>
    {
        protected Dictionary<MessageFilter, TFilterData> filters;
        protected Dictionary<MessageFilter, Candidate> candidates;
        private readonly WeakReference _processorPool;
        private int _size;
        private int _nextBit;
        private readonly Dictionary<string, HeaderBit[]> _headerLookup;
        private Dictionary<Uri, CandidateSet> _toHostLookup;
        private Dictionary<Uri, CandidateSet> _toNoHostLookup;

        internal class ProcessorPool
        {
            private EndpointAddressProcessor _processor;

            internal ProcessorPool()
            {
            }

            internal EndpointAddressProcessor Pop()
            {
                EndpointAddressProcessor p = _processor;
                if (null != p)
                {
                    _processor = (EndpointAddressProcessor)p.next;
                    p.next = null;
                    return p;
                }
                return null;
            }

            internal void Push(EndpointAddressProcessor p)
            {
                p.next = _processor;
                _processor = p;
            }
        }

        public EndpointAddressMessageFilterTable()
        {
            _processorPool = new WeakReference(null);

            _size = 0;
            _nextBit = 0;

            filters = new Dictionary<MessageFilter, TFilterData>();
            candidates = new Dictionary<MessageFilter, Candidate>();
            _headerLookup = new Dictionary<string, HeaderBit[]>();
            InitializeLookupTables();
        }

        protected virtual void InitializeLookupTables()
        {
            _toHostLookup = new Dictionary<Uri, CandidateSet>(EndpointAddressMessageFilter.HostUriComparer.Value);
            _toNoHostLookup = new Dictionary<Uri, CandidateSet>(EndpointAddressMessageFilter.NoHostUriComparer.Value);
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
                    candidates[filter].data = value;
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

        public virtual void Add(MessageFilter filter, TFilterData data)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            Add((EndpointAddressMessageFilter)filter, data);
        }

        public virtual void Add(EndpointAddressMessageFilter filter, TFilterData data)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            filters.Add(filter, data);

            // Create the candidate
            byte[] mask = BuildMask(filter.HeaderLookup);
            Candidate can = new Candidate(filter, data, mask, filter.HeaderLookup);
            candidates.Add(filter, can);

            CandidateSet cset;
            Uri soapToAddress = filter.Address.Uri;
            if (filter.IncludeHostNameInComparison)
            {
                if (!_toHostLookup.TryGetValue(soapToAddress, out cset))
                {
                    cset = new CandidateSet();
                    _toHostLookup.Add(soapToAddress, cset);
                }
            }
            else
            {
                if (!_toNoHostLookup.TryGetValue(soapToAddress, out cset))
                {
                    cset = new CandidateSet();
                    _toNoHostLookup.Add(soapToAddress, cset);
                }
            }
            cset.candidates.Add(can);

            IncrementQNameCount(cset, filter.Address);
        }

        protected void IncrementQNameCount(CandidateSet cset, EndpointAddress address)
        {
            // Update the QName ref count
            QName qname;
            for (int i = 0; i < address.Headers.Count; ++i)
            {
                AddressHeader parameter = address.Headers[i];
                qname.name = parameter.Name;
                qname.ns = parameter.Namespace;
                if (cset.qnames.TryGetValue(qname, out int cnt))
                {
                    cset.qnames[qname] = cnt + 1;
                }
                else
                {
                    cset.qnames.Add(qname, 1);
                }
            }
        }

        public void Add(KeyValuePair<MessageFilter, TFilterData> item)
        {
            Add(item.Key, item.Value);
        }

        protected byte[] BuildMask(Dictionary<string, HeaderBit[]> headerLookup)
        {
            byte[] mask = null;
            foreach (KeyValuePair<string, HeaderBit[]> item in headerLookup)
            {
                if (_headerLookup.TryGetValue(item.Key, out HeaderBit[] bits))
                {
                    if (bits.Length < item.Value.Length)
                    {
                        int old = bits.Length;
                        Array.Resize(ref bits, item.Value.Length);
                        for (int i = old; i < item.Value.Length; ++i)
                        {
                            bits[i] = new HeaderBit(_nextBit++);
                        }
                        _headerLookup[item.Key] = bits;
                    }
                }
                else
                {
                    bits = new HeaderBit[item.Value.Length];
                    for (int i = 0; i < item.Value.Length; ++i)
                    {
                        bits[i] = new HeaderBit(_nextBit++);
                    }
                    _headerLookup.Add(item.Key, bits);
                }

                for (int i = 0; i < item.Value.Length; ++i)
                {
                    bits[i].AddToMask(ref mask);
                }
            }

            if (_nextBit == 0)
            {
                _size = 0;
            }
            else
            {
                _size = (_nextBit - 1) / 8 + 1;
            }

            return mask;
        }

        public void Clear()
        {
            _size = 0;
            _nextBit = 0;
            filters.Clear();
            candidates.Clear();
            _headerLookup.Clear();
            ClearLookupTables();
        }

        protected virtual void ClearLookupTables()
        {
            if (_toHostLookup != null)
            {
                _toHostLookup.Clear();
            }
            if (_toNoHostLookup != null)
            {
                _toNoHostLookup.Clear();
            }
        }

        public bool Contains(KeyValuePair<MessageFilter, TFilterData> item)
        {
            return ((ICollection<KeyValuePair<MessageFilter, TFilterData>>)filters).Contains(item);
        }

        public bool ContainsKey(MessageFilter filter)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }
            return filters.ContainsKey(filter);
        }

        public void CopyTo(KeyValuePair<MessageFilter, TFilterData>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<MessageFilter, TFilterData>>)filters).CopyTo(array, arrayIndex);
        }

        private EndpointAddressProcessor CreateProcessor(int length)
        {
            EndpointAddressProcessor p = null;
            lock (_processorPool)
            {
                ProcessorPool pool = _processorPool.Target as ProcessorPool;
                if (null != pool)
                {
                    p = pool.Pop();
                }
            }

            if (null != p)
            {
                p.Clear(length);
                return p;
            }

            return new EndpointAddressProcessor(length);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<MessageFilter, TFilterData>> GetEnumerator()
        {
            return filters.GetEnumerator();
        }

        internal virtual bool TryMatchCandidateSet(Uri to, bool includeHostNameInComparison, out CandidateSet cset)
        {
            if (includeHostNameInComparison)
            {
                return _toHostLookup.TryGetValue(to, out cset);
            }
            else
            {
                return _toNoHostLookup.TryGetValue(to, out cset);
            }
        }

        private Candidate InnerMatch(Message message)
        {
            Uri to = message.Headers.To;
            if (to == null)
            {
                return null;
            }

            Candidate can = null;
            if (TryMatchCandidateSet(to, true/*includeHostNameInComparison*/, out CandidateSet cset))
            {
                can = GetSingleMatch(cset, message);
            }
            if (TryMatchCandidateSet(to, false/*includeHostNameInComparison*/, out cset))
            {
                Candidate c = GetSingleMatch(cset, message);
                if (c != null)
                {
                    if (can != null)
                    {
                        Collection<MessageFilter> matches = new Collection<MessageFilter>();
                        matches.Add(can.filter);
                        matches.Add(c.filter);
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MultipleFilterMatchesException(SR.FilterMultipleMatches, null, matches));
                    }
                    can = c;
                }
            }

            return can;
        }

        private Candidate GetSingleMatch(CandidateSet cset, Message message)
        {
            int candiCount = cset.candidates.Count;

            if (cset.qnames.Count == 0)
            {
                if (candiCount == 0)
                {
                    return null;
                }
                else if (candiCount == 1)
                {
                    return cset.candidates[0];
                }
                else
                {
                    Collection<MessageFilter> matches = new Collection<MessageFilter>();
                    for (int i = 0; i < candiCount; ++i)
                    {
                        matches.Add(cset.candidates[i].filter);
                    }
                    throw TraceUtility.ThrowHelperError(new MultipleFilterMatchesException(SR.FilterMultipleMatches, null, matches), message);
                }
            }

            EndpointAddressProcessor context = CreateProcessor(_size);
            context.ProcessHeaders(message, cset.qnames, _headerLookup);

            Candidate can = null;
            List<Candidate> candis = cset.candidates;
            for (int i = 0; i < candiCount; ++i)
            {
                if (context.TestMask(candis[i].mask))
                {
                    if (can != null)
                    {
                        Collection<MessageFilter> matches = new Collection<MessageFilter>();
                        matches.Add(can.filter);
                        matches.Add(candis[i].filter);
                        throw TraceUtility.ThrowHelperError(new MultipleFilterMatchesException(SR.FilterMultipleMatches, null, matches), message);
                    }
                    can = candis[i];
                }
            }

            ReleaseProcessor(context);

            return can;
        }

        private void InnerMatchData(Message message, ICollection<TFilterData> results)
        {
            Uri to = message.Headers.To;
            if (to != null)
            {
                if (TryMatchCandidateSet(to, true /*includeHostNameInComparison*/, out CandidateSet cset))
                {
                    InnerMatchData(message, results, cset);
                }
                if (TryMatchCandidateSet(to, false /*includeHostNameInComparison*/, out cset))
                {
                    InnerMatchData(message, results, cset);
                }
            }
        }

        private void InnerMatchData(Message message, ICollection<TFilterData> results, CandidateSet cset)
        {
            EndpointAddressProcessor context = CreateProcessor(_size);
            context.ProcessHeaders(message, cset.qnames, _headerLookup);

            List<Candidate> candis = cset.candidates;
            for (int i = 0; i < candis.Count; ++i)
            {
                if (context.TestMask(candis[i].mask))
                {
                    results.Add(candis[i].data);
                }
            }

            ReleaseProcessor(context);
        }

        protected void InnerMatchFilters(Message message, ICollection<MessageFilter> results)
        {
            Uri to = message.Headers.To;
            if (to != null)
            {
                if (TryMatchCandidateSet(to, true/*includeHostNameInComparison*/, out CandidateSet cset))
                {
                    InnerMatchFilters(message, results, cset);
                }
                if (TryMatchCandidateSet(to, false/*includeHostNameInComparison*/, out cset))
                {
                    InnerMatchFilters(message, results, cset);
                }
            }
        }

        private void InnerMatchFilters(Message message, ICollection<MessageFilter> results, CandidateSet cset)
        {
            EndpointAddressProcessor context = CreateProcessor(_size);
            context.ProcessHeaders(message, cset.qnames, _headerLookup);

            List<Candidate> candis = cset.candidates;
            for (int i = 0; i < candis.Count; ++i)
            {
                if (context.TestMask(candis[i].mask))
                {
                    results.Add(candis[i].filter);
                }
            }

            ReleaseProcessor(context);
        }

        public bool GetMatchingValue(Message message, out TFilterData data)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            Candidate can = InnerMatch(message);
            if (can == null)
            {
                data = default(TFilterData);
                return false;
            }

            data = can.data;
            return true;
        }

        public bool GetMatchingValue(MessageBuffer messageBuffer, out TFilterData data)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
            }

            Message msg = messageBuffer.CreateMessage();
            Candidate can = null;
            try
            {
                can = InnerMatch(msg);
            }
            finally
            {
                msg.Close();
            }

            if (can == null)
            {
                data = default(TFilterData);
                return false;
            }

            data = can.data;
            return true;
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

        public bool GetMatchingFilter(Message message, out MessageFilter filter)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            Candidate can = InnerMatch(message);
            if (can != null)
            {
                filter = can.filter;
                return true;
            }

            filter = null;
            return false;
        }

        public bool GetMatchingFilter(MessageBuffer messageBuffer, out MessageFilter filter)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
            }

            Message msg = messageBuffer.CreateMessage();
            Candidate can = null;
            try
            {
                can = InnerMatch(msg);
            }
            finally
            {
                msg.Close();
            }

            if (can != null)
            {
                filter = can.filter;
                return true;
            }

            filter = null;
            return false;
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
            InnerMatchFilters(message, results);
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
                InnerMatchFilters(msg, results);
                return count != results.Count;
            }
            finally
            {
                msg.Close();
            }
        }

        protected void RebuildMasks()
        {
            _nextBit = 0;
            _size = 0;

            // Clear out all the bits.
            _headerLookup.Clear();

            // Rebuild the masks
            foreach (Candidate can in candidates.Values)
            {
                can.mask = BuildMask(can.headerLookup);
            }
        }

        private void ReleaseProcessor(EndpointAddressProcessor processor)
        {
            lock (_processorPool)
            {
                ProcessorPool pool = _processorPool.Target as ProcessorPool;
                if (null == pool)
                {
                    pool = new ProcessorPool();
                    _processorPool.Target = pool;
                }
                pool.Push(processor);
            }
        }

        public virtual bool Remove(MessageFilter filter)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            EndpointAddressMessageFilter saFilter = filter as EndpointAddressMessageFilter;
            if (saFilter != null)
            {
                return Remove(saFilter);
            }

            return false;
        }

        public virtual bool Remove(EndpointAddressMessageFilter filter)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(filter));
            }

            if (!filters.Remove(filter))
            {
                return false;
            }

            Candidate can = candidates[filter];
            Uri soapToAddress = filter.Address.Uri;

            CandidateSet cset = null;
            if (filter.IncludeHostNameInComparison)
            {
                cset = _toHostLookup[soapToAddress];
            }
            else
            {
                cset = _toNoHostLookup[soapToAddress];
            }

            candidates.Remove(filter);

            if (cset.candidates.Count == 1)
            {
                if (filter.IncludeHostNameInComparison)
                {
                    _toHostLookup.Remove(soapToAddress);
                }
                else
                {
                    _toNoHostLookup.Remove(soapToAddress);
                }
            }
            else
            {
                DecrementQNameCount(cset, filter.Address);

                // Remove Candidate
                cset.candidates.Remove(can);
            }

            RebuildMasks();
            return true;
        }

        protected void DecrementQNameCount(CandidateSet cset, EndpointAddress address)
        {
            // Adjust QName counts
            QName qname;
            for (int i = 0; i < address.Headers.Count; ++i)
            {
                AddressHeader parameter = address.Headers[i];
                qname.name = parameter.Name;
                qname.ns = parameter.Namespace;
                int cnt = cset.qnames[qname];
                if (cnt == 1)
                {
                    cset.qnames.Remove(qname);
                }
                else
                {
                    cset.qnames[qname] = cnt - 1;
                }
            }
        }

        public bool Remove(KeyValuePair<MessageFilter, TFilterData> item)
        {
            if (((ICollection<KeyValuePair<MessageFilter, TFilterData>>)filters).Contains(item))
            {
                return Remove(item.Key);
            }
            return false;
        }

        internal class Candidate
        {
            internal MessageFilter filter;
            internal TFilterData data;
            internal byte[] mask;
            internal Dictionary<string, HeaderBit[]> headerLookup;

            internal Candidate(MessageFilter filter, TFilterData data, byte[] mask, Dictionary<string, HeaderBit[]> headerLookup)
            {
                this.filter = filter;
                this.data = data;
                this.mask = mask;
                this.headerLookup = headerLookup;
            }
        }

        internal class CandidateSet
        {
            internal Dictionary<QName, int> qnames;
            internal List<Candidate> candidates;

            internal CandidateSet()
            {
                qnames = new Dictionary<QName, int>(EndpointAddressProcessor.QNameComparer);
                candidates = new List<Candidate>();
            }
        }

        public bool TryGetValue(MessageFilter filter, out TFilterData data)
        {
            return filters.TryGetValue(filter, out data);
        }
    }
}