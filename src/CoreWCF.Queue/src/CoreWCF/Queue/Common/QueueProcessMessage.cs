// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Queue.Common
{
    internal class QueueProcessMessage
    {
        private readonly QueueMessageDispatcherDelegate _next;
        private readonly IServiceProvider _serviceProvider;

        public QueueProcessMessage(QueueMessageDispatcherDelegate next, IServiceProvider serviceProvider)
        {
            _next = next;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(QueueMessageContext queueMessageContext)
        {
            QueueInputChannel inputChannel = _serviceProvider.GetRequiredService<QueueInputChannel>();
            inputChannel.LocalAddress =
                new EndpointAddress(queueMessageContext.QueueTransportContext.ServiceDispatcher.BaseAddress);
            //await inputChannel.OpenAsync();
            var channelDispatcher =
                await queueMessageContext.QueueTransportContext.ServiceDispatcher.CreateServiceChannelDispatcherAsync(
                    inputChannel);
            await channelDispatcher.DispatchAsync(queueMessageContext);
            
            await queueMessageContext.ReplyAsync(queueMessageContext.RequestMessage);
        }
    }
}
