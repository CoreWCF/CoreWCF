using System;
using System.ServiceModel.Channels;
using RabbitMQ.Client;

namespace CoreWCF.ServiceModel.Channels
{
    public class RabbitMqTransportBindingElement : TransportBindingElement
    {
        public RabbitMqTransportBindingElement()
        { }

        private RabbitMqTransportBindingElement(RabbitMqTransportBindingElement other)
            : base(other)
        {
            MaxReceivedMessageSize = other.MaxReceivedMessageSize;
            BrokerProtocol = other.BrokerProtocol;
            SslOption = other.SslOption;
            VirtualHost = other.VirtualHost;
        }

        /// <summary>
        /// Gets the scheme used by the binding, soap.amqp
        /// </summary>
        public override string Scheme
        {
            get { return RabbitMqDefaults.Scheme; }
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
