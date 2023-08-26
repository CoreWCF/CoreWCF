// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;

namespace CoreWCF.ServiceModel.Channels
{
    public class KafkaBinding : Binding
    {
        private KafkaSecurity _security;
        private KafkaTransportBindingElement _transport;
        private TextMessageEncodingBindingElement _textEncoding;
        private BinaryMessageEncodingBindingElement _binaryEncoding;

        private KafkaMessageEncoding _messageEncoding = KafkaMessageEncoding.Text;

        public KafkaBinding()
        {
            Initialize();
        }

        public KafkaBinding(KafkaSecurityMode securityMode)
            : this()
        {
            _security.Mode = securityMode;
        }

        public override BindingElementCollection CreateBindingElements()
        {
            BindingElementCollection elements = new();

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

            return elements.Clone();
        }

        public override string Scheme => KafkaConstants.Scheme;


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

        public KafkaSecurity Security
        {
            get => _security;
            set => _security = value ?? throw new ArgumentNullException(nameof(value));
        }

        private void Initialize()
        {
            _security = new KafkaSecurity();
            _transport = new KafkaTransportBindingElement();
            _textEncoding = new TextMessageEncodingBindingElement();
            _binaryEncoding = new BinaryMessageEncodingBindingElement();
        }
    }
}
