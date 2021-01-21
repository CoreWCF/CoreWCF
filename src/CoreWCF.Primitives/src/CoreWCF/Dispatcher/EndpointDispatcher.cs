// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public class EndpointDispatcher
    {
        private MessageFilter addressFilter;
        private MessageFilter contractFilter;
        private readonly string contractNamespace;
        private MessageFilter endpointFilter;
        private readonly EndpointAddress originalAddress;

        internal EndpointDispatcher(EndpointAddress address, string contractName, string contractNamespace, string id, bool isSystemEndpoint)
            : this(address, contractName, contractNamespace)
        {
            Id = id;
            IsSystemEndpoint = isSystemEndpoint;
        }

        public EndpointDispatcher(EndpointAddress address, string contractName, string contractNamespace)
            : this(address, contractName, contractNamespace, false)
        {
        }

        public EndpointDispatcher(EndpointAddress address, string contractName, string contractNamespace, bool isSystemEndpoint)
        {
            originalAddress = address;
            ContractName = contractName;
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
            DispatchRuntime = new DispatchRuntime(this);
            FilterPriority = 0;
            IsSystemEndpoint = isSystemEndpoint;
        }

        private EndpointDispatcher(EndpointDispatcher baseEndpoint, IEnumerable<AddressHeader> headers)
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
            ContractName = baseEndpoint.ContractName;
            contractNamespace = baseEndpoint.ContractNamespace;
            DispatchRuntime = baseEndpoint.DispatchRuntime;
            // endpointFilter is lazy
            FilterPriority = baseEndpoint.FilterPriority + 1;
            originalAddress = address;
            //if (PerformanceCounters.PerformanceCountersEnabled)
            //{
            //    this.perfCounterId = baseEndpoint.perfCounterId;
            //    this.perfCounterBaseId = baseEndpoint.perfCounterBaseId;
            //}
            Id = baseEndpoint.Id;
        }

        public MessageFilter AddressFilter
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
                AddressFilterSetExplicit = true;
            }
        }

        internal bool AddressFilterSetExplicit { get; private set; }

        public ChannelDispatcher ChannelDispatcher { get; private set; }

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

        public string ContractName { get; }

        public string ContractNamespace
        {
            get { return contractNamespace; }
        }

        internal ServiceChannel DatagramChannel { get; set; }

        public DispatchRuntime DispatchRuntime { get; }

        internal Uri ListenUri { get; }

        internal EndpointAddress OriginalAddress
        {
            get { return originalAddress; }
        }

        public EndpointAddress EndpointAddress
        {
            get
            {
                if (ChannelDispatcher == null)
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

        public bool IsSystemEndpoint { get; }

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

        public int FilterPriority { get; set; }

        internal string Id { get; set; }

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

            if (ChannelDispatcher != null)
            {
                Exception error = new InvalidOperationException(SR.SFxEndpointDispatcherMultipleChannelDispatcher0);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }

            ChannelDispatcher = channelDispatcher;
            // TODO: Plumb through the listening Uri
            //listenUri = channelDispatcher.Listener?.Uri;
        }

        internal void Detach(ChannelDispatcher channelDispatcher)
        {
            if (channelDispatcher == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channelDispatcher));
            }

            if (ChannelDispatcher != channelDispatcher)
            {
                Exception error = new InvalidOperationException(SR.SFxEndpointDispatcherDifferentChannelDispatcher0);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }

            //this.ReleasePerformanceCounters();
            ChannelDispatcher = null;
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

        private void ThrowIfDisposedOrImmutable()
        {
            ChannelDispatcher channelDispatcher = ChannelDispatcher;
            if (channelDispatcher != null)
            {
                channelDispatcher.ThrowIfDisposedOrImmutable();
            }
        }
    }

}