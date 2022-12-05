// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common.Configuration;

namespace CoreWCF.Queue.Common
{
    public class QueueTransportContext
    {
        public IServiceDispatcher ServiceDispatcher { get; internal set; }
        internal MessageEncoderFactory MessageEncoderFactory { get; set; }
        public QueueBaseTransportBindingElement QueueBindingElement { get; internal set; }
        public QueueMessageDispatcherDelegate QueueMessageDispatcher { get; internal set; }
        internal QueueTransportPump QueuePump { get; set; }

        public QueueTransportContext(
            IServiceDispatcher serviceDispatcher,
            MessageEncoderFactory messageEncoderFactory,
            QueueBaseTransportBindingElement bindingElement,
            QueueMessageDispatcherDelegate dispatcherDelegate,
            QueueTransportPump queueTransportPump)
        {
            ServiceDispatcher = serviceDispatcher;
            MessageEncoderFactory = messageEncoderFactory;
            QueueBindingElement = bindingElement;
            QueueMessageDispatcher = dispatcherDelegate;
            QueuePump = queueTransportPump;
        }
    }
}
