// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public class PrefixEndpointAddressMessageFilter : MessageFilter
    {
        private readonly EndpointAddressMessageFilterHelper _helper;
        private readonly UriPrefixTable<object> _addressTable;
        private readonly HostNameComparisonMode _hostNameComparisonMode;

        public PrefixEndpointAddressMessageFilter(EndpointAddress address)
            : this(address, false)
        {
        }

        public PrefixEndpointAddressMessageFilter(EndpointAddress address, bool includeHostNameInComparison)
        {
            Address = address ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(address));
            _helper = new EndpointAddressMessageFilterHelper(Address);

            _hostNameComparisonMode = includeHostNameInComparison
                ? HostNameComparisonMode.Exact
                : HostNameComparisonMode.StrongWildcard;

            _addressTable = new UriPrefixTable<object>();
            _addressTable.RegisterUri(Address.Uri, _hostNameComparisonMode, new object());
        }

        public EndpointAddress Address { get; }

        public bool IncludeHostNameInComparison
        {
            get { return (_hostNameComparisonMode == HostNameComparisonMode.Exact); }
        }

        protected internal override IMessageFilterTable<TFilterData> CreateFilterTable<TFilterData>()
        {
            return new PrefixEndpointAddressMessageFilterTable<TFilterData>();
        }

        public override bool Match(MessageBuffer messageBuffer)
        {
            if (messageBuffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(messageBuffer));
            }

            Message msg = messageBuffer.CreateMessage();
            try
            {
                return Match(msg);
            }
            finally
            {
                msg.Close();
            }
        }

        public override bool Match(Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            // To
            Uri to = message.Headers.To;

            if (to == null || !_addressTable.TryLookupUri(to, _hostNameComparisonMode, out object o))
            {
                return false;
            }

            return _helper.Match(message);
        }

        internal Dictionary<string, EndpointAddressProcessor.HeaderBit[]> HeaderLookup
        {
            get { return _helper.HeaderLookup; }
        }
    }
}