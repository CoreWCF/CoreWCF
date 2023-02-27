using System.Linq;
using System;
using System.ServiceModel.Channels;
using System.ServiceModel.Security;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using System.IdentityModel.Selectors;
using ClientCredentials = System.ServiceModel.Description.ClientCredentials;

namespace CoreWCF.ServiceModel.Channels
{
    internal class RabbitMqChannelFactory : ChannelFactoryBase<IOutputChannel>
    {
        private BufferManager _bufferManager;
        private MessageEncoderFactory _messageEncoderFactory;
        private RabbitMqTransportBindingElement _transport;
        private SecurityCredentialsManager _channelCredentials;
        private SecurityTokenManager _securityTokenManager;

        internal RabbitMqChannelFactory(RabbitMqTransportBindingElement transport, BindingContext context)
            : base(context.Binding)
        {
            _transport = transport;
            _bufferManager = BufferManager.CreateBufferManager(transport.MaxBufferPoolSize, int.MaxValue);
            InitializeSecurityTokenManager(context);

            var messageEncoderBindingElement = context.BindingParameters.Find<MessageEncodingBindingElement>();
            _messageEncoderFactory = messageEncoderBindingElement == null
                ? RabbitMqDefaults.DefaultMessageEncoderFactory
                : messageEncoderBindingElement.CreateMessageEncoderFactory();
        }

        private void InitializeSecurityTokenManager(BindingContext context)
        {
            _channelCredentials = context.BindingParameters.OfType<SecurityCredentialsManager>().FirstOrDefault();
            _channelCredentials ??= new ClientCredentials();
            _securityTokenManager = _channelCredentials.CreateSecurityTokenManager();
        }

        public RabbitMqTransportBindingElement Transport => _transport;

        public BufferManager BufferManager => _bufferManager;

        public MessageEncoderFactory MessageEncoderFactory => _messageEncoderFactory;

        public override T GetProperty<T>()
        {
            T messageEncoderProperty = MessageEncoderFactory.Encoder.GetProperty<T>();
            if (messageEncoderProperty != null)
            {
                return messageEncoderProperty;
            }

            if (typeof(T) == typeof(MessageVersion))
            {
                return (T)(object)MessageEncoderFactory.Encoder.MessageVersion;
            }

            return base.GetProperty<T>();
        }

        protected override void OnOpen(TimeSpan timeout)
        {
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return Task.CompletedTask.ToApm(callback, state);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            result.ToApmEnd();
        }

        protected override IOutputChannel OnCreateChannel(System.ServiceModel.EndpointAddress queueUrl, Uri via)
        {
            return new RabbitMqOutputChannel(
                this,
                _transport,
                _securityTokenManager,
                MessageEncoderFactory.Encoder);
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            _bufferManager.Clear();
        }
    }
}
