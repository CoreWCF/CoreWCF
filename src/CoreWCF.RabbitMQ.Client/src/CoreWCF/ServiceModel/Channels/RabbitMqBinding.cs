using System;
using RabbitMQ.Client;
using BinaryMessageEncodingBindingElement = System.ServiceModel.Channels.BinaryMessageEncodingBindingElement;
using Binding = System.ServiceModel.Channels.Binding;
using BindingElementCollection = System.ServiceModel.Channels.BindingElementCollection;
using TextMessageEncodingBindingElement = System.ServiceModel.Channels.TextMessageEncodingBindingElement;

namespace CoreWCF.ServiceModel.Channels
{
    public class RabbitMqBinding : Binding
    {
        private RabbitMqTransportBindingElement _transport;
        private TextMessageEncodingBindingElement _textMessageEncodingBindingElement;
        private BinaryMessageEncodingBindingElement _binaryMessageEncodingBindingElement;
        private RabbitMqMessageEncoding _messageEncoding = RabbitMqMessageEncoding.Text;

        public RabbitMqBinding()
        {
            _textMessageEncodingBindingElement = new TextMessageEncodingBindingElement();
            _binaryMessageEncodingBindingElement = new BinaryMessageEncodingBindingElement();

            var sslOption = new SslOption();
            var virtualHost = ConnectionFactory.DefaultVHost;
            
            _transport = new RabbitMqTransportBindingElement
            {
                SslOption = sslOption,
                VirtualHost = virtualHost
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

        /// <summary>
        /// Gets the scheme used by the binding
        /// </summary>
        public override string Scheme => _transport.Scheme;

        public RabbitMqMessageEncoding MessageEncoding
        {
            get
            {
                return _messageEncoding;
            }
            set
            {
                if (!Enum.IsDefined(typeof(RabbitMqMessageEncoding), value))
                {
                    throw new ArgumentOutOfRangeException(SR.Format(SR.InvalidEnumValue, value));
                }
                _messageEncoding = value;
            }
        }

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
