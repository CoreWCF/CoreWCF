using CoreWCF.Channels;
using CoreWCF.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Dispatcher
{
    internal class ServiceDispatcher : IServiceDispatcher
    {
        private IRequestReplyCorrelator _requestReplyCorrelator;

        public ServiceDispatcher(ChannelDispatcher channelDispatcher)
        {
            ChannelDispatcher = channelDispatcher;
            // TODO: Maybe make lazy
            _requestReplyCorrelator = new RequestReplyCorrelator();
        }

        public Uri BaseAddress => ChannelDispatcher.ListenUri;

        public Binding Binding => ChannelDispatcher.Binding;

        public ChannelDispatcher ChannelDispatcher { get; }

        public ServiceHostBase Host { get { return ChannelDispatcher.Host; } }

        public EndpointDispatcherTable Endpoints => ChannelDispatcher.EndpointDispatcherTable;

        public IList<Type> SupportedChannelTypes => ChannelDispatcher.SupportedChannelTypes;

        public object ThisLock { get; } = new object();

        public async Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel)
        {
            var sessionIdleManager = channel.GetProperty<ServiceChannel.SessionIdleManager>();
            IChannelBinder binder = null;
            if (channel is IReplyChannel)
            {
                var rcbinder = channel.GetProperty<ReplyChannelBinder>();
                rcbinder.Init(channel as IReplyChannel, BaseAddress);
                binder = rcbinder;
            }
            else if (channel is IDuplexSessionChannel)
            {
                var dcbinder = channel.GetProperty<DuplexChannelBinder>();
                dcbinder.Init(channel as IDuplexSessionChannel, _requestReplyCorrelator, BaseAddress);
                binder = dcbinder;
            }
            else if (channel is IInputChannel)
            {
                var icbinder = channel.GetProperty<InputChannelBinder>();
                icbinder.Init(channel as IInputChannel, BaseAddress);
                binder = icbinder;
            }

            // TODO: Wire up wasChannelThrottled
            var channelHandler = new ChannelHandler(Binding.MessageVersion, binder, channel.GetProperty<ServiceThrottle>(),
             this, /*wasChannelThrottled*/ false, sessionIdleManager);

            var channelDispatcher = channelHandler.GetDispatcher();
         //   channel.ChannelDispatcher = channelDispatcher;
            await channelHandler.OpenAsync();
            return channelDispatcher;
        }
    }
}
