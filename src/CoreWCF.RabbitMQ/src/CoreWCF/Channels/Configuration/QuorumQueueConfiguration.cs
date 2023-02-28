// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.Channels.Configuration
{
    public static class RabbitMqQuorumQueueXArgument
    {
        public static readonly string DeliveryLimit = "x-delivery-limit";
    }

    public class QuorumQueueConfiguration : QueueDeclareConfiguration
    {
        private int _deliveryLimit = 3;

        public override string QueueType => RabbitMqQueueType.Quorum;

        public override bool Durable
        {
            get
            {
                return true;
            }
            set
            {
                if (!value)
                {
                    throw new ArgumentException(SR.Format(SR.UnsupportedQuorumQueuePropertyValue, nameof(Durable), false));
                }
            }
        }

        public override bool Exclusive
        {
            get
            {
                return false;
            }
            set
            {
                if (value)
                {
                    throw new ArgumentException(SR.Format(SR.UnsupportedQuorumQueuePropertyValue, nameof(Exclusive), true));
                }
            }
        }
        
        public override bool GlobalQosPrefetch
        {
            get
            {
                return false;
            }
            set
            {
                if (value)
                {
                    throw new ArgumentException(SR.Format(SR.UnsupportedQuorumQueuePropertyValue, nameof(GlobalQosPrefetch), true));
                }
            }
        }
        
        public int DeliveryLimit
        {
            get
            {
                return _deliveryLimit;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException(SR.Format(SR.UnsupportedQuorumQueuePropertyValue, nameof(DeliveryLimit), value));
                }

                _deliveryLimit = value;
            }
        }

        internal override Dictionary<string, object> ToDictionary()
        {
            var queueXArgs = base.ToDictionary();
            queueXArgs[RabbitMqQuorumQueueXArgument.DeliveryLimit] = DeliveryLimit;

            return queueXArgs;
        }
    }
}
