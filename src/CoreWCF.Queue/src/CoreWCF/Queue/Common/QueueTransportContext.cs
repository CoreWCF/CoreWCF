// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common.Configuration;

namespace CoreWCF.Queue.Common
{
    public class QueueTransportContext
    {
        public IServiceDispatcher ServiceDispatcher { get; set; }
        internal MessageEncoderFactory MessageEncoderFactory { get; set; }
        internal QueueBaseTransportBindingElement QueueBindingElement { get; set; }
        internal QueueMessageDispatcherDelegate QueueMessageDispatcher { get; set; }
        internal QueueTransportPump QueuePump { get; set; }
    }
}
