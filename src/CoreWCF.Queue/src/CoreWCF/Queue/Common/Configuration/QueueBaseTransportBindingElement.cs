// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Configuration;

namespace CoreWCF.Queue.Common.Configuration
{
    public abstract class QueueBaseTransportBindingElement : TransportBindingElement
    {
        protected QueueBaseTransportBindingElement()
        {
        }

        protected QueueBaseTransportBindingElement(QueueBaseTransportBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
        }

        public virtual int ConcurrencyLevel
        {
            get { return 1; }
        }

        public abstract QueueTransportPump BuildQueueTransportPump(BindingContext context);

        public override bool CanBuildServiceDispatcher<TChannel>(BindingContext context)
        {
            return (typeof(TChannel) == typeof(IInputChannel));
        }

        public override IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context,
            IServiceDispatcher innerDispatcher)
        {
            return innerDispatcher;
        }
    }
}
