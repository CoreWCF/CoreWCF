using System;
using System.Net;
using System.ServiceModel.Channels;
using RabbitMQ.Client;

namespace CoreWCF.ServiceModel.Channels
{
    public class RabbitMqTransportBindingElement : TransportBindingElement
    {
        private const int MaxRabbitMqMessageSize = 536870912; // 512 * (2^20) = 512 MB, max message size as of RabbitMQ v3.8
        private const int DefaultMaxMessageSize = 65535;  // 64K
        private long _maxReceivedMessageSize = DefaultMaxMessageSize;

        public RabbitMqTransportBindingElement()
        { }

        private RabbitMqTransportBindingElement(RabbitMqTransportBindingElement other)
            : base(other)
        {
            MaxReceivedMessageSize = other.MaxReceivedMessageSize;
            BaseAddress = other.BaseAddress;
            BrokerProtocol = other.BrokerProtocol;
            SslOption = other.SslOption;
            VirtualHost = other.VirtualHost;
            Credentials = other.Credentials;
        }

        public Uri BaseAddress { get; internal set; }

        /// <summary>
        /// Gets the scheme used by the binding, soap.amqp
        /// </summary>
        public override string Scheme
        {
            get { return RabbitMqDefaults.Scheme; }
        }

        /// <summary>
        /// The largest receivable encoded message
        /// </summary>
        public override long MaxReceivedMessageSize
        {
            get
            {
                return _maxReceivedMessageSize;
            }

            set
            {
                if (value <= 0 || value > MaxRabbitMqMessageSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, SR.Format(SR.InvalidMaxReceivedMessageSizeValue, 1, MaxRabbitMqMessageSize));
                }

                _maxReceivedMessageSize = value;
            }
        }

        /// <summary>
        /// Specifies the version of the AMQP protocol that should be used to
        /// communicate with the broker
        /// </summary>
        public IProtocol BrokerProtocol { get; set; } = Protocols.DefaultProtocol;

        /// <summary>
        /// SSL configuration for the RabbitMQ queue
        /// </summary>
        public SslOption SslOption { get; set; }

        /// <summary>
        /// Virtual host for the RabbitMQ queue
        /// </summary>
        public string VirtualHost { get; set; }

        /// <summary>
        /// Credentials used for accessing the RabbitMQ host
        /// </summary>
        public ICredentials Credentials { get; set; }

        public override BindingElement Clone()
        {
            return new RabbitMqTransportBindingElement(this);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return context.GetInnerProperty<T>();
        }

        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return (IChannelFactory<TChannel>)new RabbitMqChannelFactory(this, context);
        }

        /// <summary>
        /// Used by higher layers to determine what types of channel factories this
        /// binding element supports. Which in this case is just IOutputChannel.
        /// </summary>
        public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
        {
            return typeof(TChannel) == typeof(IOutputChannel);
        }
    }
}
