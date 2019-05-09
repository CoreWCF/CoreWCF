using System;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal class PrefixEndpointAddressMessageFilterTable<TFilterData> : EndpointAddressMessageFilterTable<TFilterData>
    {
        UriPrefixTable<CandidateSet> toHostTable;
        UriPrefixTable<CandidateSet> toNoHostTable;

        public PrefixEndpointAddressMessageFilterTable()
            : base()
        {
        }

        protected override void InitializeLookupTables()
        {
            toHostTable = new UriPrefixTable<CandidateSet>();
            toNoHostTable = new UriPrefixTable<CandidateSet>();
        }

        public override void Add(MessageFilter filter, TFilterData data)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter");
            }

            Add((PrefixEndpointAddressMessageFilter)filter, data);
        }

        public override void Add(EndpointAddressMessageFilter filter, TFilterData data)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter");
            }

            Fx.Assert("EndpointAddressMessageFilter cannot be added to PrefixEndpointAddressMessageFilterTable");
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException("EndpointAddressMessageFilter cannot be added to PrefixEndpointAddressMessageFilterTable"));
        }

        public void Add(PrefixEndpointAddressMessageFilter filter, TFilterData data)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter");
            }

            filters.Add(filter, data);

            // Create the candidate
            byte[] mask = BuildMask(filter.HeaderLookup);
            Candidate can = new Candidate(filter, data, mask, filter.HeaderLookup);
            candidates.Add(filter, can);

            Uri soapToAddress = filter.Address.Uri;

            CandidateSet cset;
            if (!TryMatchCandidateSet(soapToAddress, filter.IncludeHostNameInComparison, out cset))
            {
                cset = new CandidateSet();
                GetAddressTable(filter.IncludeHostNameInComparison).RegisterUri(soapToAddress, GetComparisonMode(filter.IncludeHostNameInComparison), cset);
            }
            cset.candidates.Add(can);

            IncrementQNameCount(cset, filter.Address);
        }

        HostNameComparisonMode GetComparisonMode(bool includeHostNameInComparison)
        {
            return includeHostNameInComparison ? HostNameComparisonMode.Exact : HostNameComparisonMode.StrongWildcard;
        }

        UriPrefixTable<CandidateSet> GetAddressTable(bool includeHostNameInComparison)
        {
            return includeHostNameInComparison ? toHostTable : toNoHostTable;
        }

        internal override bool TryMatchCandidateSet(Uri to, bool includeHostNameInComparison, out CandidateSet cset)
        {
            return GetAddressTable(includeHostNameInComparison).TryLookupUri(to, GetComparisonMode(includeHostNameInComparison), out cset);
        }

        protected override void ClearLookupTables()
        {
            toHostTable = new UriPrefixTable<EndpointAddressMessageFilterTable<TFilterData>.CandidateSet>();
            toNoHostTable = new UriPrefixTable<EndpointAddressMessageFilterTable<TFilterData>.CandidateSet>();
        }

        public override bool Remove(MessageFilter filter)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter");
            }

            PrefixEndpointAddressMessageFilter pFilter = filter as PrefixEndpointAddressMessageFilter;
            if (pFilter != null)
            {
                return Remove(pFilter);
            }

            return false;
        }

        public override bool Remove(EndpointAddressMessageFilter filter)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter");
            }

            Fx.Assert("EndpointAddressMessageFilter cannot be removed from PrefixEndpointAddressMessageFilterTable");
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException("EndpointAddressMessageFilter cannot be removed from PrefixEndpointAddressMessageFilterTable"));
        }

        public bool Remove(PrefixEndpointAddressMessageFilter filter)
        {
            if (filter == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("filter");
            }

            if (!filters.Remove(filter))
            {
                return false;
            }

            Candidate can = candidates[filter];
            Uri soapToAddress = filter.Address.Uri;

            CandidateSet cset = null;
            if (TryMatchCandidateSet(soapToAddress, filter.IncludeHostNameInComparison, out cset))
            {
                if (cset.candidates.Count == 1)
                {
                    GetAddressTable(filter.IncludeHostNameInComparison).UnregisterUri(soapToAddress, GetComparisonMode(filter.IncludeHostNameInComparison));
                }
                else
                {
                    DecrementQNameCount(cset, filter.Address);

                    // Remove Candidate
                    cset.candidates.Remove(can);
                }
            }
            candidates.Remove(filter);

            RebuildMasks();
            return true;
        }
    }

}