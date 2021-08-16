// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class ServiceDispatcher : IServiceDispatcher
    {
        private readonly IRequestReplyCorrelator _requestReplyCorrelator;

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

        public SimpleAsyncLock ThisLock { get; } = new SimpleAsyncLock();

        public async Task<IServiceChannelDispatcher> CreateServiceChannelDispatcherAsync(IChannel channel)
        {
            ServiceChannel.SessionIdleManager sessionIdleManager = channel.GetProperty<ServiceChannel.SessionIdleManager>();
            IChannelBinder binder = null;
            if (channel is IReplyChannel)
            {
                ReplyChannelBinder rcbinder = channel.GetProperty<ReplyChannelBinder>();
                rcbinder.Init(channel as IReplyChannel, BaseAddress);
                binder = rcbinder;
            }
            else if (channel is IDuplexSessionChannel)
            {
                DuplexChannelBinder dcbinder = channel.GetProperty<DuplexChannelBinder>();
                dcbinder.Init(channel as IDuplexSessionChannel, _requestReplyCorrelator, BaseAddress);
                binder = dcbinder;
            }
            else if (channel is IInputChannel)
            {
                InputChannelBinder icbinder = channel.GetProperty<InputChannelBinder>();
                icbinder.Init(channel as IInputChannel, BaseAddress);
                binder = icbinder;
            }

            // TODO: Wire up wasChannelThrottled
            var channelHandler = new ChannelHandler(Binding.MessageVersion, binder, channel.GetProperty<ServiceThrottle>(),
             this, /*wasChannelThrottled*/ false, sessionIdleManager);

            IServiceChannelDispatcher channelDispatcher = channelHandler.GetDispatcher();
            //   channel.ChannelDispatcher = channelDispatcher;
            await channelHandler.OpenAsync();
            return channelDispatcher;
        }
    }
}
