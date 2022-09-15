using System;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Queue.CoreWCF.Queue;
using Microsoft.AspNetCore.Builder;

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

        public virtual int MaxPendingReceives { get { return 1; } }
        public abstract QueueTransportPump BuildQueueTransportPump(BindingContext context);

        public override bool CanBuildServiceDispatcher<TChannel>(BindingContext context)
        {
            return (typeof(TChannel) == typeof(IInputChannel));
        }

        public override IServiceDispatcher BuildServiceDispatcher<TChannel>(BindingContext context, IServiceDispatcher innerDispatcher)
        {
            return innerDispatcher;
        }

    }
}

