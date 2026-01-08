// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public class EndpointDispatcher
    {
        private MessageFilter _addressFilter;
        private MessageFilter _contractFilter;
        private MessageFilter _endpointFilter;
        private EndpointIdentity _identity;

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
            OriginalAddress = address;
            ContractName = contractName;
            ContractNamespace = contractNamespace;

            if (address != null)
            {
                _addressFilter = new EndpointAddressMessageFilter(address);
            }
            else
            {
                _addressFilter = new MatchAllMessageFilter();
            }

            _contractFilter = new MatchAllMessageFilter();
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

            _addressFilter = new EndpointAddressMessageFilter(address);
            // channelDispatcher is Attached
            _contractFilter = baseEndpoint.ContractFilter;
            ContractName = baseEndpoint.ContractName;
            ContractNamespace = baseEndpoint.ContractNamespace;
            DispatchRuntime = baseEndpoint.DispatchRuntime;
            // endpointFilter is lazy
            FilterPriority = baseEndpoint.FilterPriority + 1;
            OriginalAddress = address;
            //if (PerformanceCounters.PerformanceCountersEnabled)
            //{
            //    this.perfCounterId = baseEndpoint.perfCounterId;
            //    this.perfCounterBaseId = baseEndpoint.perfCounterBaseId;
            //}
            Id = baseEndpoint.Id;
        }

        public MessageFilter AddressFilter
        {
            get { return _addressFilter; }
            set
            {
                ThrowIfDisposedOrImmutable();
                _addressFilter = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                AddressFilterSetExplicit = true;
            }
        }

        internal bool AddressFilterSetExplicit { get; private set; }

        public ChannelDispatcher ChannelDispatcher { get; private set; }

        public MessageFilter ContractFilter
        {
            get { return _contractFilter; }
            set
            {
                ThrowIfDisposedOrImmutable();
                _contractFilter = value ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
            }
        }

        public string ContractName { get; }

        public string ContractNamespace { get; }

        internal ServiceChannel DatagramChannel { get; set; }

        public DispatchRuntime DispatchRuntime { get; }

        internal Uri ListenUri { get; }

        internal EndpointAddress OriginalAddress { get; }

        public EndpointAddress EndpointAddress
        {
            get
            {
                if (ChannelDispatcher == null)
                {
                    return OriginalAddress;
                }

                if ((OriginalAddress != null) && (OriginalAddress.Identity != null))
                {
                    return OriginalAddress;
                }

                if (OriginalAddress != null && _identity == null)
                {
                    return OriginalAddress;
                }

                EndpointAddressBuilder builder;
                if (OriginalAddress != null)
                {
                    builder = new EndpointAddressBuilder(OriginalAddress);
                    builder.Identity = _identity;
                    return builder.ToEndpointAddress();
                }
                else
                {
                    return null;
                }
            }
        }

        public EndpointIdentity Identity
        {
            get { return _identity; }
            set
            {
                if (value != _identity && _identity != null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(value), SR.OnlySetIdentityOnce);
                }

                if (value == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));
                }

                _identity = value;
            }
        }

        public bool IsSystemEndpoint { get; }

        internal MessageFilter EndpointFilter
        {
            get
            {
                if (_endpointFilter == null)
                {
                    MessageFilter addressFilter = _addressFilter;
                    MessageFilter contractFilter = _contractFilter;

                    // Can't optimize addressFilter similarly.
                    // AndMessageFilter tracks when the address filter matched so the correct
                    // fault can be sent back.
                    if (contractFilter is MatchAllMessageFilter)
                    {
                        _endpointFilter = addressFilter;
                    }
                    else
                    {
                        _endpointFilter = new AndMessageFilter(addressFilter, contractFilter);
                    }
                }
                return _endpointFilter;
            }
        }

        public int FilterPriority { get; set; }

        public string Id { get; set; }

        //internal string PerfCounterId
        //{
        //    get { return this.perfCounterId; }
        //}

        //internal string PerfCounterBaseId
        //{
        //    get { return this.perfCounterBaseId; }
        //}

        internal int PerfCounterInstanceId { get; set; }

        internal static EndpointDispatcher AddEndpointDispatcher(EndpointDispatcher baseEndpoint,
                                                                 IEnumerable<AddressHeader> headers)
        {
            EndpointDispatcher endpoint = new EndpointDispatcher(baseEndpoint, headers);
            baseEndpoint.ChannelDispatcher.Endpoints.Add(endpoint);
            return endpoint;
        }

        internal void Attach(ChannelDispatcher channelDispatcher)
        {
            if (ChannelDispatcher != null)
            {
                Exception error = new InvalidOperationException(SR.SFxEndpointDispatcherMultipleChannelDispatcher0);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(error);
            }

            ChannelDispatcher = channelDispatcher ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channelDispatcher));
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
