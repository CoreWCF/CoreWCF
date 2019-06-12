using System;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public class EndpointDispatcher
    {
        MessageFilter addressFilter;
        bool addressFilterSetExplicit;
        ChannelDispatcher channelDispatcher;
        MessageFilter contractFilter;
        string contractName;
        string contractNamespace;
        ServiceChannel datagramChannel;
        DispatchRuntime dispatchRuntime;
        MessageFilter endpointFilter;
        int filterPriority;
        Uri listenUri;
        EndpointAddress originalAddress;
        //string perfCounterId;
        //string perfCounterBaseId;
        string id; // for ServiceMetadataBehavior, to help get EndpointIdentity of ServiceEndpoint from EndpointDispatcher
        bool isSystemEndpoint;

        internal EndpointDispatcher(EndpointAddress address, string contractName, string contractNamespace, string id, bool isSystemEndpoint)
            : this(address, contractName, contractNamespace)
        {
            this.id = id;
            this.isSystemEndpoint = isSystemEndpoint;
        }

        public EndpointDispatcher(EndpointAddress address, string contractName, string contractNamespace)
            : this(address, contractName, contractNamespace, false)
        {
        }

        public EndpointDispatcher(EndpointAddress address, string contractName, string contractNamespace, bool isSystemEndpoint)
        {
            originalAddress = address;
            this.contractName = contractName;
            this.contractNamespace = contractNamespace;

            if (address != null)
            {
                addressFilter = new EndpointAddressMessageFilter(address);
            }
            else
            {
                addressFilter = new MatchAllMessageFilter();
            }

            contractFilter = new MatchAllMessageFilter();
            dispatchRuntime = new DispatchRuntime(this);
            filterPriority = 0;
            this.isSystemEndpoint = isSystemEndpoint;
        }

        EndpointDispatcher(EndpointDispatcher baseEndpoint, IEnumerable<AddressHeader> headers)
        {
            EndpointAddressBuilder builder = new EndpointAddressBuilder(baseEndpoint.EndpointAddress);
            foreach (AddressHeader h in headers)
            {
                builder.Headers.Add(h);
            }
            EndpointAddress address = builder.ToEndpointAddress();

            addressFilter = new EndpointAddressMessageFilter(address);
            // channelDispatcher is Attached
            contractFilter = baseEndpoint.ContractFilter;
            contractName = baseEndpoint.ContractName;
            contractNamespace = baseEndpoint.ContractNamespace;
            dispatchRuntime = baseEndpoint.DispatchRuntime;
            // endpointFilter is lazy
            filterPriority = baseEndpoint.FilterPriority + 1;
            originalAddress = address;
            //if (PerformanceCounters.PerformanceCountersEnabled)
            //{
            //    this.perfCounterId = baseEndpoint.perfCounterId;
            //    this.perfCounterBaseId = baseEndpoint.perfCounterBaseId;
            //}
            id = baseEndpoint.id;
        }

        internal MessageFilter AddressFilter
        {
            get { return addressFilter; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                ThrowIfDisposedOrImmutable();
                addressFilter = value;
                addressFilterSetExplicit = true;
            }
        }

        internal bool AddressFilterSetExplicit
        {
            get { return addressFilterSetExplicit; }
        }

        internal ChannelDispatcher ChannelDispatcher
        {
            get { return channelDispatcher; }
        }

        internal MessageFilter ContractFilter
        {
            get { return contractFilter; }
            set
            {
                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }
                ThrowIfDisposedOrImmutable();
                contractFilter = value;
            }
        }

        public string ContractName
        {
            get { return contractName; }
        }

        public string ContractNamespace
        {
            get { return contractNamespace; }
        }

        internal ServiceChannel DatagramChannel
        {
            get { return datagramChannel; }
            set { datagramChannel = value; }
        }

        public DispatchRuntime DispatchRuntime
        {
            get { return dispatchRuntime; }
        }

        internal Uri ListenUri
        {
            get { return listenUri; }
        }

        internal EndpointAddress OriginalAddress
        {
            get { return originalAddress; }
        }

        public EndpointAddress EndpointAddress
        {
            get
            {
                if (channelDispatcher == null)
                {
                    return originalAddress;
                }

                if ((originalAddress != null) && (originalAddress.Identity != null))
                {
                    return originalAddress;
                }

                if (originalAddress != null)
                {
                    return originalAddress;
                }

                EndpointAddressBuilder builder;
                if (originalAddress != null)
                {
                    builder = new EndpointAddressBuilder(originalAddress);
                    return builder.ToEndpointAddress();
                }
                else
                {
                    return null;
                }
            }
        }

        public bool IsSystemEndpoint
        {
            get { return isSystemEndpoint; }
        }

        internal MessageFilter EndpointFilter
        {
            get
            {
                if (endpointFilter == null)
                {
                    MessageFilter addressFilter = this.addressFilter;
                    MessageFilter contractFilter = this.contractFilter;

                    // Can't optimize addressFilter similarly.
                    // AndMessageFilter tracks when the address filter matched so the correct
                    // fault can be sent back.
                    if (contractFilter is MatchAllMessageFilter)
                    {
                        endpointFilter = addressFilter;
                    }
                    else
                    {
                        endpointFilter = new AndMessageFilter(addressFilter, contractFilter);
                    }
                }
                return endpointFilter;
            }
        }

        public int FilterPriority
        {
            get { return filterPriority; }
            set { filterPriority = value; }
        }

        internal string Id
        {
            get { return id; }
            set { id = value; }
        }

        //internal string PerfCounterId
        //{
        //    get { return this.perfCounterId; }
        //}

        //internal string PerfCounterBaseId
        //{
        //    get { return this.perfCounterBaseId; }
        //}

        internal int PerfCounterInstanceId { get; set; }

        static internal EndpointDispatcher AddEndpointDispatcher(EndpointDispatcher baseEndpoint,
                                                                 IEnumerable<AddressHeader> headers)
        {
            EndpointDispatcher endpoint = new EndpointDispatcher(baseEndpoint, headers);
            baseEndpoint.ChannelDispatcher.Endpoints.Add(endpoint);
            return endpoint;
        }

        internal void Attach(ChannelDispatcher channelDispatcher)
        {
            if (channelDispatcher == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channelDispatcher));
            }

            if (this.channelDispatcher != null)
            {
                Exception error = new InvalidOperationException(SR.SFxEndpointDispatcherMultipleChannelDispatcher0);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }

            this.channelDispatcher = channelDispatcher;
            // TODO: Plumb through the listening Uri
            //listenUri = channelDispatcher.Listener?.Uri;
        }

        internal void Detach(ChannelDispatcher channelDispatcher)
        {
            if (channelDispatcher == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channelDispatcher));
            }

            if (this.channelDispatcher != channelDispatcher)
            {
                Exception error = new InvalidOperationException(SR.SFxEndpointDispatcherDifferentChannelDispatcher0);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }

            //this.ReleasePerformanceCounters();
            this.channelDispatcher = null;
        }

        //internal void ReleasePerformanceCounters()
        //{
        //    if (PerformanceCounters.PerformanceCountersEnabled)
        //    {
        //        PerformanceCounters.ReleasePerformanceCountersForEndpoint(this.perfCounterId, this.perfCounterBaseId);
        //    }
        //}

        //internal bool SetPerfCounterId()
        //{
        //    Uri keyUri = null;
        //    if (null != this.ListenUri)
        //    {
        //        keyUri = this.ListenUri;
        //    }
        //    else
        //    {
        //        EndpointAddress endpointAddress = this.EndpointAddress;
        //        if (null != endpointAddress)
        //        {
        //            keyUri = endpointAddress.Uri;
        //        }
        //    }

        //    if (null != keyUri)
        //    {
        //        this.perfCounterBaseId = keyUri.AbsoluteUri.ToUpperInvariant();
        //        this.perfCounterId = this.perfCounterBaseId + "/" + contractName.ToUpperInvariant();

        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}

        void ThrowIfDisposedOrImmutable()
        {
            ChannelDispatcher channelDispatcher = this.channelDispatcher;
            if (channelDispatcher != null)
            {
                channelDispatcher.ThrowIfDisposedOrImmutable();
            }
        }
    }

}