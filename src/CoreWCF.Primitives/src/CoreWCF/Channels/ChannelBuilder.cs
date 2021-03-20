// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;

namespace CoreWCF.Channels
{
    internal class ChannelBuilder
    {
        private BindingContext _context;
        private readonly bool _isChannelDemuxerRequired = false;

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
            return ChannelDemuxer.GetTypedServiceDispatcher<TChannel>(_context);
        }

        public TypedChannelDemuxer GetTypedChannelDemuxer(Type channelType)
        {
            return ChannelDemuxer.GetTypedServiceDispatcher(channelType, _context);
        }

        public  IServiceDispatcher AddServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher) where TChannel : class, IChannel
        {
            if (!_isChannelDemuxerRequired)
                throw new Exception("ChannelDemuxerRequired is set to false");
            return ChannelDemuxer.CreateServiceDispatcher<TChannel>(innerDispatcher, _context);
        }

        public IServiceDispatcher AddServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher, ChannelDemuxerFilter filter) where TChannel : class, IChannel
        {
            if (!_isChannelDemuxerRequired)
                throw new Exception("ChannelDemuxerRequired is set to false");
            return ChannelDemuxer.CreateServiceDispatcher<TChannel>(innerDispatcher, filter, _context);
        }

        public void RemoveServiceDispatcher<TChannel>(MessageFilter filter) where TChannel : class, IChannel
        {
            if (ChannelDemuxer == null)
            {
                throw new Exception("Demuxer can't be null");
            }
            ChannelDemuxer.RemoveServiceDispatcher<TChannel>(filter, _context);
        }

        public IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher) where TChannel : class, IChannel
        {
            if (_context != null)
            {
                IServiceDispatcher listener = _context.BuildNextServiceDispatcher<TChannel>(innerDispatcher);// .BuildInnerChannelListener<TChannel>();
                // this.listenUri = listener.Uri;
                _context = null;
                return listener;
            }
            else
            {
                return Binding.BuildServiceDispatcher<TChannel>(BindingParameters, innerDispatcher);
            }
        }
    }
}
