// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Confluent.Kafka;

namespace CoreWCF.Channels
{
    public class KafkaBinding : Binding
    {
        private KafkaTransportSecurity _security = new();
        private readonly KafkaTransportBindingElement _transportBindingElement = new();
        private readonly BinaryMessageEncodingBindingElement _binaryMessageEncodingBindingElement = new();
        private readonly TextMessageEncodingBindingElement _textMessageEncodingBindingElement = new();

        public KafkaBinding(KafkaDeliverySemantics deliverySemantics = KafkaDeliverySemantics.AtLeastOnce)
        {
            _transportBindingElement.DeliverySemantics = deliverySemantics;
        }

        public override BindingElementCollection CreateBindingElements()
        {
            _security.ApplySecurity(_transportBindingElement);

            BindingElementCollection elements = new()
            {
                MessageEncoding switch
                {
                    KafkaMessageEncoding.Binary => _binaryMessageEncodingBindingElement,
                    KafkaMessageEncoding.Text => _textMessageEncodingBindingElement,
                    _ => _textMessageEncodingBindingElement
                },
                _transportBindingElement
            };

            return elements;
        }

        public override string Scheme => _transportBindingElement.Scheme;

        public string GroupId
        {
            get => _transportBindingElement.GroupId;
            set => _transportBindingElement.GroupId = value;
        }

        public KafkaDeliverySemantics DeliverySemantics
        {
            get => _transportBindingElement.DeliverySemantics;
            set => _transportBindingElement.DeliverySemantics = value;
        }

        public KafkaMessageEncoding MessageEncoding
        {
            get => _transportBindingElement.MessageEncoding;
            set => _transportBindingElement.MessageEncoding = value;
        }

        public KafkaErrorHandlingStrategy ErrorHandlingStrategy
        {
            get => _transportBindingElement.ErrorHandlingStrategy;
            set => _transportBindingElement.ErrorHandlingStrategy = value;
        }

        public string DeadLetterQueueTopic
        {
            get => _transportBindingElement.DeadLetterQueueTopic;
            set => _transportBindingElement.DeadLetterQueueTopic = value;
        }

        public AutoOffsetReset? AutoOffsetReset
        {
            get => _transportBindingElement.AutoOffsetReset;
            set => _transportBindingElement.AutoOffsetReset = value;
        }

        public IsolationLevel? IsolationLevel
        {
            get => _transportBindingElement.IsolationLevel;
            set => _transportBindingElement.IsolationLevel = value;
        }

        public KafkaTransportSecurity Security
        {
            get => _security;
            set => _security = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}
