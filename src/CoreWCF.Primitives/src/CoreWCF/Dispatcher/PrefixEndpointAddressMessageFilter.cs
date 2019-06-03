using System;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal class PrefixEndpointAddressMessageFilter : MessageFilter
    {
        EndpointAddress _address;
        EndpointAddressMessageFilterHelper _helper;
        UriPrefixTable<object> _addressTable;
        HostNameComparisonMode _hostNameComparisonMode;

        public PrefixEndpointAddressMessageFilter(EndpointAddress address)
            : this(address, false)
        {
        }

        public PrefixEndpointAddressMessageFilter(EndpointAddress address, bool includeHostNameInComparison)
        {
            if (address == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(address));
            }

            _address = address;
            _helper = new EndpointAddressMessageFilterHelper(_address);

            _hostNameComparisonMode = includeHostNameInComparison
                ? HostNameComparisonMode.Exact
                : HostNameComparisonMode.StrongWildcard;

            _addressTable = new UriPrefixTable<object>();
            _addressTable.RegisterUri(_address.Uri, _hostNameComparisonMode, new object());
        }

        public EndpointAddress Address
        {
            get { return _address; }
        }

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

            object o;
            if (to == null || !_addressTable.TryLookupUri(to, _hostNameComparisonMode, out o))
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