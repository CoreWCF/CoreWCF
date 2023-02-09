﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using RabbitMQ.Client;

namespace CoreWCF.Channels
{
    public class RabbitMqBinding : Binding
    {
        private TextMessageEncodingBindingElement _textMessageEncodingBindingElement;
        private RabbitMqTransportBindingElement _transport;
        private BinaryMessageEncodingBindingElement _binaryMessageEncodingBindingElement;
        private bool _isInitialized;

        /// <summary>
        /// Creates a new instance of the RabbitMQBinding class initialized
        /// to use the Protocols.DefaultProtocol. The broker must be set
        /// before use.
        /// </summary>
        public RabbitMqBinding()
            : this(Protocols.DefaultProtocol)
        {
        }

        /// <summary>
        /// Uses the broker specified by the given hostname and port with
        /// Protocols.DefaultProtocol.
        /// </summary>
        /// <param name="hostname">The hostname of the broker to connect to</param>
        /// <param name="port">The port of the broker to connect to</param>
        public RabbitMqBinding(string hostname, int port)
            : this(hostname, port, Protocols.DefaultProtocol)
        {
        }

        /// <summary>
        /// Uses the broker and protocol specified
        /// </summary>
        /// <param name="hostname">The hostname of the broker to connect to</param>
        /// <param name="port">The port of the broker to connect to</param>
        /// <param name="protocol">The protocol version to use</param>
        public RabbitMqBinding(string hostname, int port, IProtocol protocol)
            : this(protocol)
        {
            HostName = hostname;
            Port = port;
        }

        /// <summary>
        /// Uses the broker, login and protocol specified
        /// </summary>
        /// <param name="hostname">The hostname of the broker to connect to</param>
        /// <param name="port">The port of the broker to connect to</param>
        /// <param name="username">The broker username to connect with</param>
        /// <param name="password">The broker password to connect with</param>
        /// <param name="virtualHost">The broker virtual host</param>
        /// <param name="maxMessageSize">The largest allowable encoded message size</param>
        /// <param name="protocol">The protocol version to use</param>
        public RabbitMqBinding(string hostname, int port, string username, string password, string virtualHost,
            long maxMessageSize, IProtocol protocol)
            : this(protocol)
        {
            HostName = hostname;
            Port = port;
            _transport.Credentials = new NetworkCredential(username, password);
            _transport.VirtualHost = virtualHost;
            MaxMessageSize = maxMessageSize;
        }

        /// <summary>
        /// Uses the specified protocol. The broker must be set before use.
        /// </summary>
        /// <param name="protocol">The protocol version to use</param>
        public RabbitMqBinding(IProtocol protocol)
        {
            BrokerProtocol = protocol;
            Name = "RabbitMQBinding";
            Namespace = "http://schemas.rabbitmq.com/2007/RabbitMQ/";

            Initialize();
        }

        public override BindingElementCollection CreateBindingElements()
        {
            _transport.BrokerProtocol = BrokerProtocol;
            _transport.MaxReceivedMessageSize = TransportDefaults.DefaultMaxMessageSize;
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

        private void Initialize()
        {
            if (!_isInitialized)
            {
                _transport = new RabbitMqTransportBindingElement();
                _textMessageEncodingBindingElement = new TextMessageEncodingBindingElement();
                _binaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement();
                MaxMessageSize = TransportDefaults.DefaultMaxMessageSize;
                _isInitialized = true;
            }
        }

        /// <summary>
        /// Gets the scheme used by the binding, soap.amqp
        /// </summary>
        public override string Scheme
        {
            get { return CurrentVersion.Scheme; }
        }

        /// <summary>
        /// Specifies the hostname of the RabbitMQ Server
        /// </summary>
        public string HostName { get; set; }

        /// <summary>
        /// Specifies the RabbitMQ Server port
        /// </summary>
        public int Port { get; set; }

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
        public IProtocol BrokerProtocol { get; set; }

        public RabbitMqMessageEncoding MessageEncoding { get; set; } = RabbitMqMessageEncoding.Text;
    }
}
