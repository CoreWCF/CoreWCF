// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common.Configuration;
using CoreWCF.Queue.CoreWCF.Queue;

namespace CoreWCF.Queue.Common
{
    public class QueueTransportContext
    {
        public IServiceDispatcher ServiceDispatcher { get;  set; }
        //public IServiceChannelDispatcher ServiceChannelDispatcher { get; set; }
        public MessageEncoderFactory MessageEncoderFactory { get; set; }
        public QueueBaseTransportBindingElement QueueBindingElement { get; set; }
        public QueueMessageDispatcherDelegate QueueHandShakeDelegate { get; set; }

        public QueueTransportPump QueuePump { get; internal set; }
    }
}
