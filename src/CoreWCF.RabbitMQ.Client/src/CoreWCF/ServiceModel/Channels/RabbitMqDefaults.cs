namespace CoreWCF.ServiceModel.Channels
{
    internal static class RabbitMqDefaults
    {
        internal const string Scheme = "amqp";

        private static readonly System.ServiceModel.Channels.MessageEncoderFactory _messageEncoderFactory;

        static RabbitMqDefaults()
        {
            _messageEncoderFactory = new System.ServiceModel.Channels.TextMessageEncodingBindingElement().CreateMessageEncoderFactory();
        }

        internal static System.ServiceModel.Channels.MessageEncoderFactory DefaultMessageEncoderFactory => _messageEncoderFactory;
    }
}
