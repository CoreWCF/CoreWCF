// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Dispatcher;

namespace CoreWCF.Channels
{
    internal class ChannelBuilder
    {
        private readonly bool _isChannelDemuxerRequired = false;
        private Uri _listenUriBaseAddress;
        private BindingContext _context;

        public ChannelBuilder(BindingContext context, bool addChannelDemuxerIfRequired)
        {
            _context = context;
            _isChannelDemuxerRequired = addChannelDemuxerIfRequired;
            if (_isChannelDemuxerRequired)
            {
                ChannelDemuxer = new ChannelDemuxer();
            }

            Binding = new CustomBinding(context.Binding, context.RemainingBindingElements);
            BindingParameters = context.BindingParameters;
        }

        public ChannelBuilder(Binding binding, BindingParameterCollection bindingParameters, bool addChannelDemuxerIfRequired)
        {
            Binding = new CustomBinding(binding);
            BindingParameters = bindingParameters;
            _isChannelDemuxerRequired = addChannelDemuxerIfRequired;
        }

        public CustomBinding Binding { get; set; }

        public BindingParameterCollection BindingParameters { get; set; }

        public ChannelDemuxer ChannelDemuxer { get; }

        public TypedChannelDemuxer GetTypedChannelDemuxer<TChannel>() where TChannel : class, IChannel
        {
            return ChannelDemuxer.GetTypedServiceDispatcher<TChannel>(BindingParameters);
        }

        public TypedChannelDemuxer GetTypedChannelDemuxer(Type channelType)
        {
            return ChannelDemuxer.GetTypedServiceDispatcher(channelType, BindingParameters);
        }

        public  IServiceDispatcher AddServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher) where TChannel : class, IChannel
        {
            if (!_isChannelDemuxerRequired)
                throw new Exception("ChannelDemuxerRequired is set to false");
            return ChannelDemuxer.CreateServiceDispatcher<TChannel>(innerDispatcher, BindingParameters);
        }

        public IServiceDispatcher AddServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher, ChannelDemuxerFilter filter) where TChannel : class, IChannel
        {
            if (!_isChannelDemuxerRequired)
                throw new Exception("ChannelDemuxerRequired is set to false");
            return ChannelDemuxer.CreateServiceDispatcher<TChannel>(innerDispatcher, filter, BindingParameters);
        }

        public void RemoveServiceDispatcher<TChannel>(MessageFilter filter) where TChannel : class, IChannel
        {
            if (ChannelDemuxer == null)
            {
                throw new Exception("Demuxer can't be null");
            }
            ChannelDemuxer.RemoveServiceDispatcher<TChannel>(filter, BindingParameters);
        }

        public IServiceDispatcher BuildServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher) where TChannel : class, IChannel
        {
            if (_context != null)
            {
                IServiceDispatcher serviceDispatcher = _context.BuildNextServiceDispatcher<TChannel>(innerDispatcher);
                _listenUriBaseAddress = serviceDispatcher.BaseAddress;
                _context = null;
                return serviceDispatcher;
            }
            else
            {
                BindingContext context = new BindingContext(new CustomBinding(Binding), BindingParameters, _listenUriBaseAddress, string.Empty);
                return context.BuildNextServiceDispatcher<TChannel>(innerDispatcher);
            }
        }

        public bool CanBuildServiceDispatcher<TChannel>() where TChannel : class, IChannel
        {
            return Binding.CanBuildServiceDispatcher<TChannel>(BindingParameters);
        }
    }
}
