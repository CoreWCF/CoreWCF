// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using CoreWCF.Channels.Configuration;
using RabbitMQ.Client;

namespace CoreWCF.Channels
{
    public class RabbitMqBinding : Binding
    {
        private RabbitMqTransportBindingElement _transport;
        private TextMessageEncodingBindingElement _textMessageEncodingBindingElement;
        private BinaryMessageEncodingBindingElement _binaryMessageEncodingBindingElement;

        public RabbitMqBinding()
        {
            Name = "RabbitMQBinding";
            Namespace = "http://schemas.rabbitmq.com/2007/RabbitMQ/";
            
            var sslOption = new SslOption();
            var virtualHost = ConnectionFactory.DefaultVHost;
            var queueConfiguration = new DefaultQueueConfiguration();

            _textMessageEncodingBindingElement = new TextMessageEncodingBindingElement();
            _binaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement();
            _transport = new RabbitMqTransportBindingElement
            {
                SslOption = sslOption,
                VirtualHost = virtualHost,
                QueueConfiguration = queueConfiguration
            };
        }

        /// <summary>
        /// Specifies the maximum encoded message size
        /// </summary>
        public long MaxMessageSize
        {
            get => _transport.MaxReceivedMessageSize;
            set => _transport.MaxReceivedMessageSize = value;
        }

        /// <summary>
        /// Specifies the version of the AMQP protocol that should be used to communicate with the broker
        /// </summary>
        public IProtocol BrokerProtocol
        {
            get => _transport.BrokerProtocol;
            set => _transport.BrokerProtocol = value;
        }

        public SslOption SslOption
        {
            get => _transport.SslOption;
            set => _transport.SslOption = value;
        }

        public string VirtualHost
        {
            get => _transport.VirtualHost;
            set => _transport.VirtualHost = value;
        }

        /// <summary>
        /// Credentials to access the RabbitMQ host
        /// </summary>
        public ICredentials Credentials
        {
            get => _transport.Credentials;
            set => _transport.Credentials = value;
        }

        /// <summary>
        /// Configuration used for declaring a queue
        /// </summary>
        public QueueDeclareConfiguration QueueConfiguration
        {
            get => _transport.QueueConfiguration;
            set => _transport.QueueConfiguration = value;
        }

        /// <summary>
        /// Gets the scheme used by the binding
        /// </summary>
        public override string Scheme => _transport.Scheme;

        public RabbitMqMessageEncoding MessageEncoding { get; set; } = RabbitMqMessageEncoding.Text;

        public override BindingElementCollection CreateBindingElements()
        {
            BindingElementCollection elements = new BindingElementCollection();

            switch (MessageEncoding)
            {
                case RabbitMqMessageEncoding.Binary:
                    elements.Add(_binaryMessageEncodingBindingElement);
                    break;
                case RabbitMqMessageEncoding.Text:
                    elements.Add(_textMessageEncodingBindingElement);
                    break;
                default:
                    elements.Add(_textMessageEncodingBindingElement);
                    break;
            }

            elements.Add(_transport);

            return elements;
        }
    }
}
