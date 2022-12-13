// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Confluent.Kafka;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    public partial class KafkaTransportBindingElement : QueueBaseTransportBindingElement
    {
        private KafkaMessageEncoding _messageEncoding = KafkaMessageEncoding.Text;
        private KafkaDeliverySemantics _deliverySemantics;
        private KafkaErrorHandlingStrategy _errorHandlingStrategy = KafkaErrorHandlingStrategy.Ignore;
        internal ConsumerConfig Config { get; } = new();

        public KafkaDeliverySemantics DeliverySemantics
        {
            get => _deliverySemantics;
            set
            {
                if (!KafkaDeliverySemanticsHelper.IsDefined(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _deliverySemantics = value;
            }
        }

        public KafkaMessageEncoding MessageEncoding
        {
            get => _messageEncoding;
            set
            {
                if (!KafkaMessageEncodingHelper.IsDefined(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _messageEncoding = value;
            }
        }

        public KafkaErrorHandlingStrategy ErrorHandlingStrategy
        {
            get => _errorHandlingStrategy;
            set
            {
                if (!KafkaErrorHandlingStrategyHelper.IsDefined(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _errorHandlingStrategy = value;
            }
        }

        public string DeadLetterQueueTopic { get; set; }

        public KafkaTransportBindingElement()
        {
        }

        private KafkaTransportBindingElement(KafkaTransportBindingElement other)
            : this()
        {
            _deliverySemantics = other.DeliverySemantics;
            _messageEncoding = other.MessageEncoding;
            Config = other.Config;
        }

        public override BindingElement Clone()
        {
            return new KafkaTransportBindingElement(this);
        }

        public override string Scheme => "net.kafka";

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (typeof(T) == typeof(ISecurityCapabilities))
            {
                return null;
            }

            return base.GetProperty<T>(context);
        }

        public override QueueTransportPump BuildQueueTransportPump(BindingContext context)
        {
            var serviceProvider = context.BindingParameters.Find<IServiceProvider>();
            var serviceDispatcher = context.BindingParameters.Find<IServiceDispatcher>();
            var logger = serviceProvider.GetRequiredService<ILogger<KafkaTransportPump>>();

            return new KafkaTransportPump(this, logger, serviceDispatcher, DeliverySemantics);
        }
    }
}
