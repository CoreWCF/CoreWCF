using System;
using CoreWCF.Channels;
using CoreWCF.Queue.CoreWCF.Queue;

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


    }
}

