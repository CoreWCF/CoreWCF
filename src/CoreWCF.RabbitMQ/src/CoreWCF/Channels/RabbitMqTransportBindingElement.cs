// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using RabbitMQ.Client;

namespace CoreWCF.Channels
{
    public class RabbitMqTransportBindingElement : TransportBindingElement
    {
        /// <summary>
        /// Creates a new instance of the RabbitMQTransportBindingElement Class using the default protocol.
        /// </summary>
        public RabbitMqTransportBindingElement()
        {
            MaxReceivedMessageSize = RabbitMqBinding.DefaultMaxMessageSize;
        }

        private RabbitMqTransportBindingElement(RabbitMqTransportBindingElement other)
        {
            HostName = other.HostName;
            Port = other.Port;
            BrokerProtocol = other.BrokerProtocol;
            Username = other.Username;
            Password = other.Password;
            VirtualHost = other.VirtualHost;
            MaxReceivedMessageSize = other.MaxReceivedMessageSize;
        }


        public override BindingElement Clone()
        {
            return new RabbitMqTransportBindingElement(this);
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
        /// The largest receivable encoded message
        /// </summary>
        public override long MaxReceivedMessageSize { get; set; }

        /// <summary>
        /// The username  to use when authenticating with the broker
        /// </summary>
        internal string Username { get; set; }

        /// <summary>
        /// Password to use when authenticating with the broker
        /// </summary>
        internal string Password { get; set; }

        /// <summary>
        /// Specifies the broker virtual host
        /// </summary>
        internal string VirtualHost { get; set; }

        /// <summary>
        /// Specifies the version of the AMQP protocol that should be used to
        /// communicate with the broker
        /// </summary>
        public IProtocol BrokerProtocol { get; set; }
    }
}
