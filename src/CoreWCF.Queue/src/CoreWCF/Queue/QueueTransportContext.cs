// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Configuration;

namespace CoreWCF.Queue
{
    public class QueueTransportContext
    {
        public IServiceDispatcher ServiceDispatcher { get;  set; }
        public IServiceChannelDispatcher ServiceChannelDispatcher { get; set; }
        public MessageEncoder MessageEncoder { get; set; }
        public Binding Binding { get; set; }
        public QueueMessageDispatch QueueHandShakeDelegate { get; set; }
    }
}
