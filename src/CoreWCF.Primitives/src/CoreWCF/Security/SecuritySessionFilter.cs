using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;

namespace CoreWCF.Security
{
    internal sealed class SecuritySessionFilter : HeaderFilter
    {
        private static readonly string SessionContextIdsProperty = String.Format(CultureInfo.InvariantCulture, "{0}/SecuritySessionContextIds", DotNetSecurityStrings.Namespace);
        private UniqueId securityContextTokenId;
        private SecurityStandardsManager standardsManager;
        private string[] excludedActions;
        private bool isStrictMode;

        public SecuritySessionFilter(UniqueId securityContextTokenId, SecurityStandardsManager standardsManager, bool isStrictMode, params string[] excludedActions)
        {
            if (securityContextTokenId == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(securityContextTokenId)));
            }

            this.excludedActions = excludedActions;
            this.securityContextTokenId = securityContextTokenId;
            this.standardsManager = standardsManager;
            this.isStrictMode = isStrictMode;
        }

        public UniqueId SecurityContextTokenId
        {
            get
            {
                return this.securityContextTokenId;
            }
        }

        private static bool ShouldExcludeMessage(Message message, string[] excludedActions)
        {
            string action = message.Headers.Action;
            if (excludedActions == null || action == null)
            {
                return false;
            }
            for (int i = 0; i < excludedActions.Length; ++i)
            {
                if (String.Equals(action, excludedActions[i], StringComparison.Ordinal))
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
            if (ShouldExcludeMessage(message, this.excludedActions))
            {
                return false;
            }
            List<UniqueId> contextIds;
            object propertyValue;
            if (!message.Properties.TryGetValue(SessionContextIdsProperty, out propertyValue))
            {
                contextIds = new List<UniqueId>(1);
                try
                {
                    if (!this.standardsManager.TryGetSecurityContextIds(message, message.Version.Envelope.UltimateDestinationActorValues, this.isStrictMode, contextIds))
                    {
                        return false;
                    }
                }
                catch (Exception e)
                {
                    if (!CanHandleException(e)) throw;
                    return false;
                }
                message.Properties.Add(SessionContextIdsProperty, contextIds);
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
                if (contextIds[i] == this.securityContextTokenId)
                {
                    message.Properties.Remove(SessionContextIdsProperty);
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
            return new SecuritySessionFilterTable<FilterData>(this.standardsManager, this.isStrictMode, this.excludedActions);
        }

        private class SecuritySessionFilterTable<FilterData> : IMessageFilterTable<FilterData>
        {
            private Dictionary<UniqueId, KeyValuePair<MessageFilter, FilterData>> contextMappings;
            private Dictionary<MessageFilter, FilterData> filterMappings;
            private SecurityStandardsManager standardsManager;
            private string[] excludedActions;
            private bool isStrictMode;

            public SecuritySessionFilterTable(SecurityStandardsManager standardsManager, bool isStrictMode, string[] excludedActions)
            {
                if (standardsManager == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(standardsManager));
                }
                if (excludedActions == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(excludedActions));
                }
                this.standardsManager = standardsManager;
                this.excludedActions = new string[excludedActions.Length];
                excludedActions.CopyTo(this.excludedActions, 0);
                this.isStrictMode = isStrictMode;
                contextMappings = new Dictionary<UniqueId, KeyValuePair<MessageFilter, FilterData>>();
                filterMappings = new Dictionary<MessageFilter, FilterData>();
            }

            public ICollection<MessageFilter> Keys
            {
                get
                {
                    return this.filterMappings.Keys;
                }
            }

            public ICollection<FilterData> Values
            {
                get
                {
                    return this.filterMappings.Values;
                }
            }

            public FilterData this[MessageFilter filter]
            {
                get
                {
                    return this.filterMappings[filter];
                }
                set
                {
                    if (this.filterMappings.ContainsKey(filter))
                    {
                        this.Remove(filter);
                    }
                    this.Add(filter, value);
                }
            }

            public int Count
            {
                get { return this.filterMappings.Count; }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }

            public void Add(KeyValuePair<MessageFilter, FilterData> item)
            {
                this.Add(item.Key, item.Value);
            }

            public void Clear()
            {
                this.filterMappings.Clear();
                this.contextMappings.Clear();
            }

            public bool Contains(KeyValuePair<MessageFilter, FilterData> item)
            {
                return this.ContainsKey(item.Key);
            }

            public void CopyTo(KeyValuePair<MessageFilter, FilterData>[] array, int arrayIndex)
            {
                int pos = arrayIndex;
                foreach (KeyValuePair<MessageFilter, FilterData> entry in this.contextMappings.Values)
                {
                    array[pos] = entry;
                    ++pos;
                }
            }

            public bool Remove(KeyValuePair<MessageFilter, FilterData> item)
            {
                return this.Remove(item.Key);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }

            public IEnumerator<KeyValuePair<MessageFilter, FilterData>> GetEnumerator()
            {
                return ((ICollection<KeyValuePair<MessageFilter, FilterData>>)this.contextMappings.Values).GetEnumerator();
            }

            public void Add(MessageFilter filter, FilterData data)
            {
                SecuritySessionFilter sessionFilter = filter as SecuritySessionFilter;
                if (sessionFilter == null)
                {
                    Fx.Assert(String.Format(CultureInfo.InvariantCulture, "Unknown filter type {0}", filter.GetType()));
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnknownFilterType, filter.GetType())));
                }
                if (sessionFilter.standardsManager != this.standardsManager)
                {
                    Fx.Assert("Standards manager of filter does not match that of filter table");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.StandardsManagerDoesNotMatch));
                }
                if (sessionFilter.isStrictMode != this.isStrictMode)
                {
                    Fx.Assert("Session filter's isStrictMode differs from filter table's isStrictMode");
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.FilterStrictModeDifferent));
                }
                if (this.contextMappings.ContainsKey(sessionFilter.SecurityContextTokenId))
                {
                    Fx.Assert(SR.Format(SR.SecuritySessionIdAlreadyPresentInFilterTable, sessionFilter.SecurityContextTokenId));
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SecuritySessionIdAlreadyPresentInFilterTable, sessionFilter.SecurityContextTokenId)));
                }
                this.filterMappings.Add(filter, data);
                this.contextMappings.Add(sessionFilter.SecurityContextTokenId, new KeyValuePair<MessageFilter, FilterData>(filter, data));
            }

            public bool ContainsKey(MessageFilter filter)
            {
                return this.filterMappings.ContainsKey(filter);
            }

            public bool Remove(MessageFilter filter)
            {
                SecuritySessionFilter sessionFilter = filter as SecuritySessionFilter;
                if (sessionFilter == null)
                {
                    return false;
                }
                bool result = this.filterMappings.Remove(filter);
                if (result)
                {
                    this.contextMappings.Remove(sessionFilter.SecurityContextTokenId);
                }
                return result;
            }

            public bool TryGetValue(MessageFilter filter, out FilterData data)
            {
                return this.filterMappings.TryGetValue(filter, out data);
            }

            private bool TryGetContextIds(Message message, out List<UniqueId> contextIds)
            {
                object propertyValue;
                if (!message.Properties.TryGetValue(SessionContextIdsProperty, out propertyValue))
                {
                    contextIds = new List<UniqueId>(1);
                    return this.standardsManager.TryGetSecurityContextIds(message, message.Version.Envelope.UltimateDestinationActorValues,
                        isStrictMode, contextIds);
                }
                else
                {
                    contextIds = propertyValue as List<UniqueId>;
                    return (contextIds != null);
                }
            }

            private bool TryMatchCore(Message message, out KeyValuePair<MessageFilter, FilterData> match)
            {
                match = default(KeyValuePair<MessageFilter, FilterData>);
                if (ShouldExcludeMessage(message, this.excludedActions))
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
                    if (!SecuritySessionFilter.CanHandleException(e)) throw;
                    return false;
                }
                for (int i = 0; i < contextIds.Count; ++i)
                {
                    if (this.contextMappings.TryGetValue(contextIds[i], out match))
                    {
                        message.Properties.Remove(SessionContextIdsProperty);
                        return true;
                    }
                }
                return false;
            }

            public bool GetMatchingValue(Message message, out FilterData data)
            {
                KeyValuePair<MessageFilter, FilterData> matchingPair;
                if (!TryMatchCore(message, out matchingPair))
                {
                    data = default(FilterData);
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
                FilterData matchingData;
                if (!GetMatchingValue(message, out matchingData))
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
                KeyValuePair<MessageFilter, FilterData> matchingPair;
                if (!TryMatchCore(message, out matchingPair))
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
                MessageFilter match;
                if (GetMatchingFilter(message, out match))
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
