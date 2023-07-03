// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Confluent.Kafka;

namespace CoreWCF.Channels
{
    public class KafkaBinding : Binding
    {
        private KafkaSecurity _security = new();
        private KafkaTransportBindingElement _transport;
        private BinaryMessageEncodingBindingElement _binaryEncoding;
        private TextMessageEncodingBindingElement _textEncoding;

        public KafkaBinding()
        {
            Initialize();
        }

        public KafkaBinding(KafkaDeliverySemantics deliverySemantics)
            : this()
        {
            _transport.DeliverySemantics = deliverySemantics;
        }

        public KafkaBinding(KafkaSecurityMode securityMode, KafkaDeliverySemantics deliverySemantics = KafkaDeliverySemantics.AtLeastOnce)
            : this(deliverySemantics)
        {
            _security.Mode = securityMode;
        }

        private void Initialize()
        {
            _transport = new KafkaTransportBindingElement();
            _binaryEncoding = new BinaryMessageEncodingBindingElement();
            _textEncoding = new TextMessageEncodingBindingElement();
        }

        public override BindingElementCollection CreateBindingElements()
        {
            BindingElementCollection elements = new();

            // TODO: Add Message security.
            SecurityBindingElement securityBindingElement = _security.CreateMessageSecurity();
            if (securityBindingElement != null)
            {
                elements.Add(securityBindingElement);
            }

            MessageEncodingBindingElement encodingBindingElement = MessageEncoding switch
            {
                KafkaMessageEncoding.Binary => _binaryEncoding,
                KafkaMessageEncoding.Text => _textEncoding,
                _ => _textEncoding
            };

            elements.Add(encodingBindingElement);

            _security.ApplySecurity(_transport);

            elements.Add(_transport);

            return elements;
        }

        public override string Scheme => _transport.Scheme;

        public string GroupId
        {
            get => _transport.GroupId;
            set => _transport.GroupId = value;
        }

        public KafkaDeliverySemantics DeliverySemantics
        {
            get => _transport.DeliverySemantics;
            set => _transport.DeliverySemantics = value;
        }

        public KafkaMessageEncoding MessageEncoding
        {
            get => _transport.MessageEncoding;
            set => _transport.MessageEncoding = value;
        }

        public KafkaErrorHandlingStrategy ErrorHandlingStrategy
        {
            get => _transport.ErrorHandlingStrategy;
            set => _transport.ErrorHandlingStrategy = value;
        }

        public string DeadLetterQueueTopic
        {
            get => _transport.DeadLetterQueueTopic;
            set => _transport.DeadLetterQueueTopic = value;
        }

        public AutoOffsetReset? AutoOffsetReset
        {
            get => _transport.AutoOffsetReset;
            set => _transport.AutoOffsetReset = value;
        }

        public IsolationLevel? IsolationLevel
        {
            get => _transport.IsolationLevel;
            set => _transport.IsolationLevel = value;
        }

        public KafkaSecurity Security
        {
            get => _security;
            set => _security = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}
