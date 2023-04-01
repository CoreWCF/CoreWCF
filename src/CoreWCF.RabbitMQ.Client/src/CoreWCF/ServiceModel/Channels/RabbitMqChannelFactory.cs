using System;
using System.ServiceModel.Channels;
using System.ServiceModel.Security;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;
using System.Net.Http;
using ClientCredentials = System.ServiceModel.Description.ClientCredentials;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace CoreWCF.ServiceModel.Channels
{
    internal class RabbitMqChannelFactory : ChannelFactoryBase<IOutputChannel>
    {
        private SecurityCredentialsManager _channelCredentials;
        private SecurityTokenManager _securityTokenManager;
        private X509CertificateValidator _sslCertificateValidator;
        private Func<HttpRequestMessage, X509Certificate2, X509Chain, SslPolicyErrors, bool> _remoteCertificateValidationCallback;

        internal RabbitMqChannelFactory(RabbitMqTransportBindingElement transport, BindingContext context)
            : base(context.Binding)
        {
            Transport = transport;
            BufferManager = BufferManager.CreateBufferManager(transport.MaxBufferPoolSize, int.MaxValue);
            InitializeSecurityTokenManager(context);

            // TODO: Uncomment when .GetCertificateValidator() becomes available (currently internal to System.ServiceModel.Primitives)
            //ClientCredentials credentials = context.BindingParameters.Find<ClientCredentials>();
            //if (credentials != null && credentials.ServiceCertificate.SslCertificateAuthentication != null)
            //{
            //    _sslCertificateValidator = credentials.ServiceCertificate.SslCertificateAuthentication.GetCertificateValidator();
            //    _remoteCertificateValidationCallback = RemoteCertificateValidationCallback;
            //}

            var messageEncoderBindingElement = context.BindingParameters.Find<MessageEncodingBindingElement>();
            MessageEncoderFactory = messageEncoderBindingElement == null
                ? RabbitMqDefaults.DefaultMessageEncoderFactory
                : messageEncoderBindingElement.CreateMessageEncoderFactory();
        }

        private void InitializeSecurityTokenManager(BindingContext context)
        {
            _channelCredentials = context.BindingParameters.Find<SecurityCredentialsManager>();
            _channelCredentials ??= new ClientCredentials();
            _securityTokenManager = _channelCredentials.CreateSecurityTokenManager();
        }

        public RabbitMqTransportBindingElement Transport { get; }

        public BufferManager BufferManager { get; }

        public MessageEncoderFactory MessageEncoderFactory { get; }

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

        protected override IOutputChannel OnCreateChannel(System.ServiceModel.EndpointAddress endpointAddress, Uri via)
        {
            return new RabbitMqOutputChannel(
                this,
                endpointAddress,
                via,
                Transport,
                _securityTokenManager,
                MessageEncoderFactory.Encoder);
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            BufferManager.Clear();
        }

        private bool RemoteCertificateValidationCallback(HttpRequestMessage sender, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            if (_sslCertificateValidator == null)
            {
                throw new Exception(SR.SslCertificateValidatorIsNull);
            }

            try
            {
                _sslCertificateValidator.Validate(certificate);
                return true;
            }
            catch (SecurityTokenValidationException ex)
            {
                return false;
            }
        }
    }
}
