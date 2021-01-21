// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;

namespace CoreWCF.Channels
{
    internal class ChannelBuilder
    {
        private BindingContext context;
        private readonly Uri listenUri;
        private readonly bool isChannelDemuxerRequired = false;

        public ChannelBuilder(BindingContext context, bool addChannelDemuxerIfRequired)
        {
            this.context = context;
            isChannelDemuxerRequired = addChannelDemuxerIfRequired;
            if (isChannelDemuxerRequired)
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
            isChannelDemuxerRequired = addChannelDemuxerIfRequired;

        }

        public CustomBinding Binding { get; set; }

        public BindingParameterCollection BindingParameters { get; set; }

        public ChannelDemuxer ChannelDemuxer { get; }
        public IServiceDispatcher AddServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher) where TChannel : class, IChannel
        {
            if (!isChannelDemuxerRequired)
            {
                throw new Exception("ChannelDemuxerRequired is set to false");
            }

            return ChannelDemuxer.CreaterServiceDispatcher<TChannel>(innerDispatcher);
        }

        public IServiceDispatcher AddServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher, ChannelDemuxerFilter filter) where TChannel : class, IChannel
        {
            if (!isChannelDemuxerRequired)
            {
                throw new Exception("ChannelDemuxerRequired is set to false");
            }

            return ChannelDemuxer.CreaterServiceDispatcher<TChannel>(innerDispatcher, filter);
        }

        public void RemoveServiceDispatcher<TChannel>(MessageFilter filter) where TChannel : class, IChannel
        {
            if (ChannelDemuxer == null)
            {
                throw new Exception("Demuxer can't be null");
            }

            ChannelDemuxer.RemoveServiceDispatcher<TChannel>(filter);
        }

        public IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher) where TChannel : class, IChannel
        {
            if (this.context != null)
            {
                IServiceDispatcher listener = this.context.BuildNextServiceDispatcher<TChannel>(innerDispatcher);// .BuildInnerChannelListener<TChannel>();
                // this.listenUri = listener.Uri;
                this.context = null;
                return listener;
            }
            else
            {
                return Binding.BuildServiceDispatcher<TChannel>(BindingParameters, innerDispatcher);
            }
        }
    }
}
