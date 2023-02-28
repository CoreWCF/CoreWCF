// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace CoreWCF.Channels.Configuration
{
    public static class RabbitMqQueueType
    {
        public static readonly string Classic = "classic";
        public static readonly string Quorum = "quorum";
    }

    public static class RabbitMqQueueXArgument
    {
        public static readonly string QueueType = "x-queue-type";
        public static readonly string Priority = "x-max-priority";
        public static readonly string DeadLetterExchange = "x-dead-letter-exchange ";
    }

    public abstract class QueueDeclareConfiguration
    {
        /// <summary>
        /// Declares the type of queue to be created
        /// </summary>
        public abstract string QueueType { get; }

        /// <summary>
        /// If non-zero, the queue will behave as a priority queue, where the value of Priority is the max priority value the queue should support.
        /// </summary>
        public virtual byte Priority { get; set; } = 0;

        /// <summary>
        /// If a message is dead-lettered, it will be republished to this exchange
        /// </summary>
        public virtual string DeadLetterExchange { get; set; } = "dead-letter-exchange";

        /// <summary>
        /// If true, the queue will survive a broker restart.
        /// </summary>
        public virtual bool Durable { get; set; } = true;

        /// <summary>
        /// If true, the queue will be limited to its declaring connection and deleted when its declaring connection closes.
        /// </summary>
        public virtual bool Exclusive { get; set; } = false;

        /// <summary>
        /// If true, the queue will be auto-deleted when its last consumer (if any) unsubscribes.
        /// </summary>
        public virtual bool AutoDelete { get; set; } = false;

        /// <summary>
        /// Sets number of messages to receive from queue
        /// </summary>
        private ushort _prefetchCount = 1;
        public virtual ushort PrefetchCount
        {
            get
            {
                return _prefetchCount;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(SR.Format(SR.InvalidRabbitMqPrefetchCountValue, value));
                }
                _prefetchCount = value;
            }
        }

        /// <summary>
        /// If true, PrefetchCount is shared by all consumers. If false, the PrefetchCount is per channel/consumer.
        /// </summary>
        public virtual bool GlobalQosPrefetch { get; set; } = false;

        /// <summary>
        /// Creates a dictionary of x-arguments used to define a RabbitMQ queue.
        /// </summary>
        /// <returns>A dictionary of x-arguments and their values</returns>
        internal virtual Dictionary<string, object> ToDictionary()
        {
            var propertiesDict = new Dictionary<string, object>
            {
                { RabbitMqQueueXArgument.QueueType, QueueType },
                { RabbitMqQueueXArgument.DeadLetterExchange, DeadLetterExchange }
            };

            if (Priority > 0)
            {
                propertiesDict[RabbitMqQueueXArgument.Priority] = Priority;
            }

            return propertiesDict;
        }
    }
}
