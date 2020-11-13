using System;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;

namespace CoreWCF.Channels
{
    internal class ChannelBuilder
    {
        private CustomBinding binding;
        private BindingContext context;
        private BindingParameterCollection bindingParameters;
        private Uri listenUri;
        private ChannelDemuxer channelDemuxer;
        private bool isChannelDemuxerRequired = false;

        public ChannelBuilder(BindingContext context, bool addChannelDemuxerIfRequired)
        {
            this.context = context;
            isChannelDemuxerRequired = addChannelDemuxerIfRequired;
            if (isChannelDemuxerRequired)
                channelDemuxer = new ChannelDemuxer();
            this.binding = new CustomBinding(context.Binding, context.RemainingBindingElements);
            this.bindingParameters = context.BindingParameters;
        }

        public ChannelBuilder(Binding binding, BindingParameterCollection bindingParameters, bool addChannelDemuxerIfRequired)
        {
            this.binding = new CustomBinding(binding);
            this.bindingParameters = bindingParameters;
            isChannelDemuxerRequired = addChannelDemuxerIfRequired;

        }

        public CustomBinding Binding
        {
            get { return this.binding; }
            set { this.binding = value; }
        }

        public BindingParameterCollection BindingParameters
        {
            get { return this.bindingParameters; }
            set { this.bindingParameters = value; }
        }
        
        public ChannelDemuxer ChannelDemuxer
        {
            get { return channelDemuxer; }
        }
        public  IServiceDispatcher AddServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher) where TChannel : class, IChannel
        {
            if (!isChannelDemuxerRequired)
                throw new Exception("ChannelDemuxerRequired is set to false");
            return channelDemuxer.CreaterServiceDispatcher<TChannel>(innerDispatcher);
        }

        public IServiceDispatcher AddServiceDispatcher<TChannel>(IServiceDispatcher innerDispatcher, ChannelDemuxerFilter filter) where TChannel : class, IChannel
        {
            if (!isChannelDemuxerRequired)
                throw new Exception("ChannelDemuxerRequired is set to false");
            return channelDemuxer.CreaterServiceDispatcher<TChannel>(innerDispatcher, filter);
        }

        public void RemoveServiceDispatcher<TChannel>(MessageFilter filter) where TChannel : class, IChannel
        {
            if (channelDemuxer == null)
                throw new Exception("Demuxer can't be null");
            channelDemuxer.RemoveServiceDispatcher<TChannel>(filter);
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
                return this.binding.BuildServiceDispatcher<TChannel>(this.bindingParameters, innerDispatcher);
            }
        }
        /*
        void AddDemuxerBindingElement(BindingElementCollection elements)
        {
            if (elements.Find<ChannelDemuxerBindingElement>() == null)
            {
                // add the channel demuxer binding element right above the transport
                TransportBindingElement transport = elements.Find<TransportBindingElement>();
                if (transport == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.GetString(SR.TransportBindingElementNotFound)));
                }
                // cache the context state in the demuxer so that the same context state can be provided to the transport
                // when building auxilliary channels and listeners (for ex, for security negotiation)
                elements.Insert(elements.IndexOf(transport), new ChannelDemuxerBindingElement(true));
            }
        }*/

        /* public IChannelFactory<TChannel> BuildChannelFactory<TChannel>()
         {
             if (this.context != null)
             {
                 IChannelFactory<TChannel> factory = this.context.BuildInnerChannelFactory<TChannel>();
                 this.context = null;
                 return factory;
             }
             else
             {
                 return this.binding.BuildChannelFactory<TChannel>(this.bindingParameters);
             }
         }*/

        /*
         public IChannelListener<TChannel> BuildChannelListener<TChannel>() where TChannel : class, IChannel
         {
             if (this.context != null)
             {
                 IChannelListener<TChannel> listener = this.context.BuildInnerChannelListener<TChannel>();
                 this.listenUri = listener.Uri;
                 this.context = null;
                 return listener;
             }
             else
             {
                 return this.binding.BuildChannelListener<TChannel>(this.listenUri, this.bindingParameters);
             }
         }

         public IChannelListener<TChannel> BuildChannelListener<TChannel>(MessageFilter filter, int priority) where TChannel : class, IChannel
         {
             this.bindingParameters.Add(new ChannelDemuxerFilter(filter, priority));
             IChannelListener<TChannel> listener = this.BuildChannelListener<TChannel>();
             this.bindingParameters.Remove<ChannelDemuxerFilter>();

             return listener;
         }

         public bool CanBuildChannelFactory<TChannel>()
         {
             return this.binding.CanBuildChannelFactory<TChannel>(this.bindingParameters);
         }

         public bool CanBuildChannelListener<TChannel>() where TChannel : class, IChannel
         {
             return this.binding.CanBuildChannelListener<TChannel>(this.bindingParameters);
         }*/
    }
}
