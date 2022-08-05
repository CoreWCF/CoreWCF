// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading.Tasks;
using CoreWCF.Channels;

namespace CoreWCF.Queue
{
    public class QueueMessageContext
    {
        public PipeReader Reader { get; set; }
        public IDictionary<string, object> Properties { get; set; }
        public Message RequestMessage { get; set; }
        public QueueTransportContext QueueTransportContext { get; set; }
        public EndpointAddress LocalAddress { get; set; }
    }

    public delegate Task QueueMessageDispatch(QueueMessageContext context);
}
