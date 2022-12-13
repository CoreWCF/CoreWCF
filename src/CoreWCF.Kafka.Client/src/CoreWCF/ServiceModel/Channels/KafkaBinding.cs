// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;

namespace CoreWCF.ServiceModel.Channels
{
    public class KafkaBinding : Binding
    {
        private KafkaTransportSecurity _security;
        private KafkaTransportBindingElement _transportBindingElement;
        private TextMessageEncodingBindingElement _textMessageEncodingBindingElement;
        private BinaryMessageEncodingBindingElement _binaryMessageEncodingBindingElement;

        private KafkaMessageEncoding _messageEncoding = KafkaMessageEncoding.Text;

        public KafkaBinding()
        {
            Initialize();
        }

        public override BindingElementCollection CreateBindingElements()
        {
            _security.Apply(_transportBindingElement);

            BindingElementCollection bindingElements = new()
            {
                MessageEncoding switch
                {
                    KafkaMessageEncoding.Binary => _binaryMessageEncodingBindingElement,
                    KafkaMessageEncoding.Text => _textMessageEncodingBindingElement,
                    _ => _textMessageEncodingBindingElement
                },
                _transportBindingElement
            };

            return bindingElements.Clone();
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

        public KafkaTransportSecurity Security
        {
            get => _security;
            set => _security = value ?? throw new ArgumentNullException(nameof(value));
        }

        private void Initialize()
        {
            _security = new KafkaTransportSecurity();
            _transportBindingElement = new KafkaTransportBindingElement();
            _textMessageEncodingBindingElement = new TextMessageEncodingBindingElement();
            _binaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement();
        }
    }
}
