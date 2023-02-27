using System;
using System.IdentityModel.Selectors;
using System.IdentityModel.Tokens;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Security.Tokens;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CoreWCF.ServiceModel.Channels
{
    public class RabbitMqOutputChannel : ChannelBase, IOutputChannel
    {
        private readonly RabbitMqChannelFactory _parent;
        private readonly RabbitMqTransportBindingElement _transport;
        private readonly SecurityTokenManager _securityTokenManager;
        private readonly EndpointAddress _baseAddress;
        private readonly Uri _via;
        private readonly MessageEncoder _encoder;
        private RabbitMqConnectionSettings _connectionSettings;
        private IModel _rabbitMqClient;

        internal RabbitMqOutputChannel(
            RabbitMqChannelFactory factory,
            RabbitMqTransportBindingElement transport,
            SecurityTokenManager securityTokenManager,
            MessageEncoder encoder)
            : base(factory)
        {
            _parent = factory;
            _transport = transport;
            _securityTokenManager = securityTokenManager;
            _encoder = encoder;
            
            _via = _transport.BaseAddress;
            _baseAddress = new EndpointAddress(_via);
        }

        EndpointAddress IOutputChannel.RemoteAddress => _baseAddress;

        Uri IOutputChannel.Via => _via;

        protected ChannelParameterCollection ChannelParameters { get; private set; }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IOutputChannel))
            {
                return (T)(object)this;
            }

            if (typeof(T) == typeof(ChannelParameterCollection))
            {
                if (State == CommunicationState.Created)
                {
                    lock (ThisLock)
                    {
                        if (ChannelParameters == null)
                        {
                            ChannelParameters = new ChannelParameterCollection();
                        }
                    }
                }
                return (T)(object)ChannelParameters;
            }

            T messageEncoderProperty = _encoder.GetProperty<T>();
            if (messageEncoderProperty != null)
            {
                return messageEncoderProperty;
            }

            return base.GetProperty<T>();
        }

        /// <summary>
        /// Open the channel for use. We do not have any blocking work to perform so this is a no-op
        /// </summary>
        protected override void OnOpen(TimeSpan timeout)
        {
            OnOpenAsync(timeout).Wait();
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return OnOpenAsync(timeout).ToApm(callback, state);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            result.ToApmEnd();
        }

        protected override void OnAbort()
        { }

        protected override void OnClose(TimeSpan timeout)
        {
            OnCloseAsync().Wait();
        }

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return OnCloseAsync().ToApm(callback, state);
        }

        protected override void OnEndClose(IAsyncResult result)
        {
            result.ToApmEnd();
        }

        public void Send(Message message)
        {
            SendAsync(message).Wait();
        }

        public void Send(Message message, TimeSpan timeout)
        {
            SendAsync(message, timeout).Wait();
        }

        public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
        {
            return SendAsync(message).ToApm(callback, state);
        }

        public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            return SendAsync(message, timeout).ToApm(callback, state);
        }

        public void EndSend(IAsyncResult result)
        {
            result.ToApmEnd();
        }

        /// <summary>
        /// Address the Message and serialize it into a byte array.
        /// </summary>
        private ArraySegment<byte> EncodeMessage(Message message)
        {
            try
            {
                _baseAddress.ApplyTo(message);
                return _encoder.WriteMessage(
                    message,
                    (int)_parent.Transport.MaxReceivedMessageSize,
                    _parent.BufferManager);
            }
            finally
            {
                // We have consumed the message by serializing it, so clean up
                message.Close();
            }
        }

        private async Task OnOpenAsync(TimeSpan timeout)
        {
            string userName = ConnectionFactory.DefaultUser;
            string password = ConnectionFactory.DefaultPass;

            var token = await GetSecurityTokenAsync(timeout);
            if (token != null)
            {
                // When UserNameSecurityToken is made public, cast token to UserNameSecurityToken
                // and read UserName and Password properties directly instead of using reflection.
                userName = GetPropertyValueFromReflection(token, "UserName");
                password = GetPropertyValueFromReflection(token, "Password");
            }

            _connectionSettings = GetConnectionSettings(userName, password);
            var factory = _connectionSettings.GetConnectionFactory();
            var connection = factory.CreateConnection();
            _rabbitMqClient = connection.CreateModel();
            _rabbitMqClient.ConfirmSelect();
        }

        private string GetPropertyValueFromReflection(SecurityToken token, string propertyName)
        {
            var tokenType = token.GetType();
            var propertyValue = tokenType.GetProperty(propertyName)?.GetValue(token, null);
            return propertyValue as string ?? string.Empty;
        }

        /// <summary>
        /// Published a Message to RabbitMQ.
        /// </summary>
        /// <exception cref="TimeoutException"></exception>
        private Task SendAsync(Message message)
        {
            return SendAsync(message, TimeSpan.Zero);
        }

        /// <summary>
        /// Published a Message to RabbitMQ and waits for a Publisher confirm.
        /// If timeout is less than or equal to zero, RabbitMQ will client will not
        /// wait for a publish confirmation from the RabbitMQ broker.
        /// Note: Waiting for the publish confirmation could impact performance.
        /// </summary>
        /// <exception cref="TimeoutException"></exception>
        private Task SendAsync(Message message, TimeSpan timeout)
        {
            var messageBuffer = EncodeMessage(message);

            try
            {
                if (!_rabbitMqClient.IsOpen)
                {
                    _rabbitMqClient.Abort();
                    Abort();
                    throw new CommunicationException(SR.RabbitMqClientNotOpen);
                }

                _rabbitMqClient.BasicPublish(
                    exchange: _connectionSettings.Exchange,
                    routingKey: _connectionSettings.RoutingKey,
                    body: messageBuffer);
                if (timeout.Ticks > 0)
                {
                    _rabbitMqClient.WaitForConfirmsOrDie(timeout);
                }
            }
            catch (OperationInterruptedException e)
            {
                throw new TimeoutException(SR.Format(SR.RabbitMqWaitTimeExceeded, timeout.Milliseconds));
            }
            finally
            {
                // Make sure buffers are always returned to the BufferManager
                _parent.BufferManager.ReturnBuffer(messageBuffer.Array);
            }

            return Task.CompletedTask;
        }

        private Task OnCloseAsync()
        {
            _rabbitMqClient.Close();
            return Task.CompletedTask;
        }

        private async Task<SecurityToken> GetSecurityTokenAsync(TimeSpan timeout)
        {
            var securityTokenProvider = GetSecurityTokenProvider();
            var token = await securityTokenProvider?.GetTokenAsync(timeout);
            return token;
        }

        private SecurityTokenProvider GetSecurityTokenProvider()
        {
            if (_securityTokenManager == null)
            {
                return null;
            }

            var usernameRequirement = new InitiatorServiceModelSecurityTokenRequirement
            {
                // Replace hardcoded value for TokenType when changes to make enum SecurityTokenTypes public are published
                // https://github.com/dotnet/wcf/blob/main/src/System.ServiceModel.Primitives/src/System/IdentityModel/Tokens/SecurityTokenTypes.cs#L11
                TokenType = "http://schemas.microsoft.com/ws/2006/05/identitymodel/tokens/UserName",
                RequireCryptographicToken = false,
                TargetAddress = _baseAddress,
                Via = _via,
                TransportScheme = _transport.Scheme
            };

            var channelParameters = GetProperty<ChannelParameterCollection>();
            if (channelParameters != null)
            {
                usernameRequirement.Properties[ServiceModelSecurityTokenRequirement.ChannelParametersCollectionProperty] = channelParameters;
            }
            var securityTokenProvider = _securityTokenManager.CreateSecurityTokenProvider(usernameRequirement);

            return securityTokenProvider;
        }

        private RabbitMqConnectionSettings GetConnectionSettings(string userName, string password)
        {
            return RabbitMqConnectionSettings.FromUri(_transport.BaseAddress, userName, password, _transport.SslOption, _transport.VirtualHost);
        }
    }
}
