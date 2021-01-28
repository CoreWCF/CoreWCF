// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;

namespace CoreWCF.Dispatcher
{
    internal class EndpointDispatcherTable
    {
        private MessageFilterTable<EndpointDispatcher> _filters;
        private const int optimizationThreshold = 2;
        private List<EndpointDispatcher> _cachedEndpoints;

        public EndpointDispatcherTable(object thisLock)
        {
            ThisLock = thisLock;
        }

        public int Count
        {
            get
            {
                return ((_cachedEndpoints != null) ? _cachedEndpoints.Count : 0) +
                    ((_filters != null) ? _filters.Count : 0);
            }
        }

        private object ThisLock { get; }

        public void AddEndpoint(EndpointDispatcher endpoint)
        {
            lock (ThisLock)
            {
                MessageFilter filter = endpoint.EndpointFilter;
                int priority = endpoint.FilterPriority;

                if (_filters == null)
                {
                    if (_cachedEndpoints == null)
                    {
                        _cachedEndpoints = new List<EndpointDispatcher>(optimizationThreshold);
                    }

                    if (_cachedEndpoints.Count < optimizationThreshold)
                    {
                        _cachedEndpoints.Add(endpoint);
                    }
                    else
                    {
                        _filters = new MessageFilterTable<EndpointDispatcher>();
                        for (int i = 0; i < _cachedEndpoints.Count; i++)
                        {
                            int cachedPriority = _cachedEndpoints[i].FilterPriority;
                            MessageFilter cachedFilter = _cachedEndpoints[i].EndpointFilter;
                            _filters.Add(cachedFilter, _cachedEndpoints[i], cachedPriority);
                        }
                        _filters.Add(filter, endpoint, priority);
                        _cachedEndpoints = null;
                    }
                }
                else
                {
                    _filters.Add(filter, endpoint, priority);
                }
            }
        }

        public void RemoveEndpoint(EndpointDispatcher endpoint)
        {
            lock (ThisLock)
            {
                if (_filters == null)
                {
                    if (_cachedEndpoints != null && _cachedEndpoints.Contains(endpoint))
                    {
                        _cachedEndpoints.Remove(endpoint);
                    }
                }
                else
                {
                    MessageFilter filter = endpoint.EndpointFilter;
                    _filters.Remove(filter);
                }
            }
        }

        private EndpointDispatcher LookupInCache(Message message, out bool addressMatched)
        {
            EndpointDispatcher result = null;
            int priority = int.MinValue;
            bool duplicatePriority = false;
            addressMatched = false;

            if (_cachedEndpoints != null && _cachedEndpoints.Count > 0)
            {
                for (int i = 0; i < _cachedEndpoints.Count; i++)
                {
                    EndpointDispatcher cachedEndpoint = _cachedEndpoints[i];
                    int cachedPriority = cachedEndpoint.FilterPriority;
                    MessageFilter cachedFilter = cachedEndpoint.EndpointFilter;

                    bool matchResult;
                    AndMessageFilter andFilter = cachedFilter as AndMessageFilter;
                    if (andFilter != null)
                    {
                        matchResult = andFilter.Match(message, out bool addressResult);
                        addressMatched |= addressResult;
                    }
                    else
                    {
                        matchResult = cachedFilter.Match(message);
                    }

                    if (matchResult)
                    {
                        addressMatched = true;
                        if (cachedPriority > priority || result == null)
                        {
                            result = cachedEndpoint;
                            priority = cachedPriority;
                            duplicatePriority = false;
                        }
                        else if (cachedPriority == priority && result != null)
                        {
                            duplicatePriority = true;
                        }
                    }
                }
            }

            if (duplicatePriority)
            {
                throw TraceUtility.ThrowHelperError(new MultipleFilterMatchesException(SR.FilterMultipleMatches), message);
            }

            return result;
        }

        public EndpointDispatcher Lookup(Message message, out bool addressMatched)
        {
            EndpointDispatcher data = null;

            data = LookupInCache(message, out addressMatched);

            if (data == null)
            {
                lock (ThisLock)
                {
                    data = LookupInCache(message, out addressMatched);

                    if (data == null && _filters != null)
                    {
                        _filters.GetMatchingValue(message, out data, out addressMatched);
                    }
                }
            }

            return data;
        }
    }
}