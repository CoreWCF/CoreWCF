// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;

namespace CoreWCF.Security
{
    internal sealed class SecuritySessionFilter : HeaderFilter
    {
        private static readonly string s_sessionContextIdsProperty = string.Format(CultureInfo.InvariantCulture, "{0}/SecuritySessionContextIds", DotNetSecurityStrings.Namespace);
        private readonly SecurityStandardsManager _standardsManager;
        private readonly string[] _excludedActions;
        private readonly bool _isStrictMode;

        public SecuritySessionFilter(UniqueId securityContextTokenId, SecurityStandardsManager standardsManager, bool isStrictMode, params string[] excludedActions)
        {
            _excludedActions = excludedActions;
            SecurityContextTokenId = securityContextTokenId ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(securityContextTokenId)));
            _standardsManager = standardsManager;
            _isStrictMode = isStrictMode;
        }

        public UniqueId SecurityContextTokenId { get; }

        private static bool ShouldExcludeMessage(Message message, string[] excludedActions)
        {
            string action = message.Headers.Action;
            if (excludedActions == null || action == null)
            {
                return false;
            }
            for (int i = 0; i < excludedActions.Length; ++i)
            {
                if (string.Equals(action, excludedActions[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool CanHandleException(Exception e)
        {
            return ((e is XmlException)
                    || (e is FormatException)
                    || (e is SecurityTokenException)
                    || (e is MessageSecurityException)
                    || (e is ProtocolException)
                    || (e is InvalidOperationException)
                    || (e is ArgumentException));
        }

        public override bool Match(Message message)
        {
            if (ShouldExcludeMessage(message, _excludedActions))
            {
                return false;
            }
            List<UniqueId> contextIds;
            if (!message.Properties.TryGetValue(s_sessionContextIdsProperty, out object propertyValue))
            {
                contextIds = new List<UniqueId>(1);
                try
                {
                    if (!_standardsManager.TryGetSecurityContextIds(message, message.Version.Envelope.UltimateDestinationActorValues, _isStrictMode, contextIds))
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    if (!CanHandleException(e))
                    {
                        throw;
                    }

                    return false;
                }
                message.Properties.Add(s_sessionContextIdsProperty, contextIds);
            }
            else
            {
                contextIds = (propertyValue as List<UniqueId>);
                if (contextIds == null)
                {
                    return false;
                }
            }
            for (int i = 0; i < contextIds.Count; ++i)
            {
                if (contextIds[i] == SecurityContextTokenId)
                {
                    message.Properties.Remove(s_sessionContextIdsProperty);
                    return true;
                }
            }
            return false;
        }

        public override bool Match(MessageBuffer buffer)
        {
            using (Message message = buffer.CreateMessage())
            {
                return Match(message);
            }
        }

        protected internal override IMessageFilterTable<FilterData> CreateFilterTable<FilterData>()
        {
            return new SecuritySessionFilterTable<FilterData>(_standardsManager, _isStrictMode, _excludedActions);
        }

        private class SecuritySessionFilterTable<FilterData> : IMessageFilterTable<FilterData>
        {
            private readonly Dictionary<UniqueId, KeyValuePair<MessageFilter, FilterData>> _contextMappings;
            private readonly Dictionary<MessageFilter, FilterData> _filterMappings;
            private readonly SecurityStandardsManager _standardsManager;
            private readonly string[] _excludedActions;
            private readonly bool _isStrictMode;

            public SecuritySessionFilterTable(SecurityStandardsManager standardsManager, bool isStrictMode, string[] excludedActions)
            {
                if (excludedActions == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(excludedActions));
                }
                _standardsManager = standardsManager ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(standardsManager));
                _excludedActions = new string[excludedActions.Length];
                excludedActions.CopyTo(_excludedActions, 0);
                _isStrictMode = isStrictMode;
                _contextMappings = new Dictionary<UniqueId, KeyValuePair<MessageFilter, FilterData>>();
                _filterMappings = new Dictionary<MessageFilter, FilterData>();
            }

            public ICollection<MessageFilter> Keys
            {
                get
                {
                    return _filterMappings.Keys;
                }
            }

            public ICollection<FilterData> Values
            {
                get
                {
                    return _filterMappings.Values;
                }
            }

            public FilterData this[MessageFilter filter]
            {
                get
                {
                    return _filterMappings[filter];
                }
                set
                {
                    if (_filterMappings.ContainsKey(filter))
                    {
                        Remove(filter);
                    }
                    Add(filter, value);
                }
            }

            public int Count
            {
                get { return _filterMappings.Count; }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }

            public void Add(KeyValuePair<MessageFilter, FilterData> item)
            {
                Add(item.Key, item.Value);
            }

            public void Clear()
            {
                _filterMappings.Clear();
                _contextMappings.Clear();
            }

            public bool Contains(KeyValuePair<MessageFilter, FilterData> item)
            {
                return ContainsKey(item.Key);
            }

            public void CopyTo(KeyValuePair<MessageFilter, FilterData>[] array, int arrayIndex)
            {
                int pos = arrayIndex;
                foreach (KeyValuePair<MessageFilter, FilterData> entry in _contextMappings.Values)
                {
                    array[pos] = entry;
                    ++pos;
                }
            }

            public bool Remove(KeyValuePair<MessageFilter, FilterData> item)
            {
                return Remove(item.Key);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public IEnumerator<KeyValuePair<MessageFilter, FilterData>> GetEnumerator()
            {
                return ((ICollection<KeyValuePair<MessageFilter, FilterData>>)_contextMappings.Values).GetEnumerator();
            }

            public void Add(MessageFilter filter, FilterData data)
            {
                if (!(filter is SecuritySessionFilter sessionFilter))
                {
                    Fx.Assert(string.Format(CultureInfo.InvariantCulture, "Unknown filter type {0}", filter.GetType()));
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnknownFilterType, filter.GetType())));
                }
                if (sessionFilter._standardsManager != _standardsManager)
                {
                    Fx.Assert("Standards manager of filter does not match that of filter table");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.StandardsManagerDoesNotMatch));
                }
                if (sessionFilter._isStrictMode != _isStrictMode)
                {
                    Fx.Assert("Session filter's isStrictMode differs from filter table's isStrictMode");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.FilterStrictModeDifferent));
                }
                if (_contextMappings.ContainsKey(sessionFilter.SecurityContextTokenId))
                {
                    Fx.Assert(SR.Format(SR.SecuritySessionIdAlreadyPresentInFilterTable, sessionFilter.SecurityContextTokenId));
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySessionIdAlreadyPresentInFilterTable, sessionFilter.SecurityContextTokenId)));
                }
                _filterMappings.Add(filter, data);
                _contextMappings.Add(sessionFilter.SecurityContextTokenId, new KeyValuePair<MessageFilter, FilterData>(filter, data));
            }

            public bool ContainsKey(MessageFilter filter)
            {
                return _filterMappings.ContainsKey(filter);
            }

            public bool Remove(MessageFilter filter)
            {
                if (!(filter is SecuritySessionFilter sessionFilter))
                {
                    return false;
                }
                bool result = _filterMappings.Remove(filter);
                if (result)
                {
                    _contextMappings.Remove(sessionFilter.SecurityContextTokenId);
                }
                return result;
            }

            public bool TryGetValue(MessageFilter filter, out FilterData data)
            {
                return _filterMappings.TryGetValue(filter, out data);
            }

            private bool TryGetContextIds(Message message, out List<UniqueId> contextIds)
            {
                if (!message.Properties.TryGetValue(s_sessionContextIdsProperty, out object propertyValue))
                {
                    contextIds = new List<UniqueId>(1);
                    return _standardsManager.TryGetSecurityContextIds(message, message.Version.Envelope.UltimateDestinationActorValues,
                        _isStrictMode, contextIds);
                }
                else
                {
                    contextIds = propertyValue as List<UniqueId>;
                    return (contextIds != null);
                }
            }

            private bool TryMatchCore(Message message, out KeyValuePair<MessageFilter, FilterData> match)
            {
                match = default;
                if (ShouldExcludeMessage(message, _excludedActions))
                {
                    return false;
                }
                List<UniqueId> contextIds;
                try
                {
                    if (!TryGetContextIds(message, out contextIds))
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    if (!CanHandleException(e))
                    {
                        throw;
                    }

                    return false;
                }
                for (int i = 0; i < contextIds.Count; ++i)
                {
                    if (_contextMappings.TryGetValue(contextIds[i], out match))
                    {
                        message.Properties.Remove(s_sessionContextIdsProperty);
                        return true;
                    }
                }
                return false;
            }

            public bool GetMatchingValue(Message message, out FilterData data)
            {
                if (!TryMatchCore(message, out KeyValuePair<MessageFilter, FilterData> matchingPair))
                {
                    data = default;
                    return false;
                }
                data = matchingPair.Value;
                return true;
            }

            public bool GetMatchingValue(MessageBuffer buffer, out FilterData data)
            {
                using (Message message = buffer.CreateMessage())
                {
                    return GetMatchingValue(message, out data);
                }
            }

            public bool GetMatchingValues(Message message, ICollection<FilterData> results)
            {
                if (!GetMatchingValue(message, out FilterData matchingData))
                {
                    return false;
                }
                results.Add(matchingData);
                return true;
            }

            public bool GetMatchingValues(MessageBuffer buffer, ICollection<FilterData> results)
            {
                using (Message message = buffer.CreateMessage())
                {
                    return GetMatchingValues(message, results);
                }
            }

            public bool GetMatchingFilter(Message message, out MessageFilter filter)
            {
                if (!TryMatchCore(message, out KeyValuePair<MessageFilter, FilterData> matchingPair))
                {
                    filter = null;
                    return false;
                }
                filter = matchingPair.Key;
                return true;
            }

            public bool GetMatchingFilter(MessageBuffer buffer, out MessageFilter filter)
            {
                using (Message message = buffer.CreateMessage())
                {
                    return GetMatchingFilter(message, out filter);
                }
            }

            public bool GetMatchingFilters(Message message, ICollection<MessageFilter> results)
            {
                if (GetMatchingFilter(message, out MessageFilter match))
                {
                    results.Add(match);
                    return true;
                }
                return false;
            }

            public bool GetMatchingFilters(MessageBuffer buffer, ICollection<MessageFilter> results)
            {
                using (Message message = buffer.CreateMessage())
                {
                    return GetMatchingFilters(message, results);
                }
            }
        }
    }
}
