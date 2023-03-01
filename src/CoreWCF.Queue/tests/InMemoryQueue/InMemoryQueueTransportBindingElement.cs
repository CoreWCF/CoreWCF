// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Queue.Tests.InMemoryQueue;

public class InMemoryQueueTransportBindingElement : QueueBaseTransportBindingElement
{
    public InMemoryQueueTransportBindingElement()
    {

    }

    public InMemoryQueueTransportBindingElement(InMemoryQueueTransportBindingElement other)
    {

    }

    public override BindingElement Clone() => new InMemoryQueueTransportBindingElement(this);

    public override string Scheme => "inmem://";
    public InMemoryMessageEncoding MessageEncoding { get; set; } = InMemoryMessageEncoding.Text;

    public override QueueTransportPump BuildQueueTransportPump(BindingContext context)
    {
        var serviceProvider = context.BindingParameters.Find<IServiceProvider>();
        var queue = serviceProvider.GetRequiredService<ConcurrentQueue<string>>();
        var receiveContextInterceptor = serviceProvider.GetRequiredService<ReceiveContextInterceptor>();
        return new InMemoryQueueTransportPump(queue, receiveContextInterceptor);
    }
}
