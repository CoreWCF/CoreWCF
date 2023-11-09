// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using CoreWCF.Runtime;

namespace CoreWCF.ServiceModel.Channels
{
    internal class KafkaOutputChannel : ChannelBase, IOutputChannel
    {
        private readonly MessageEncoder _encoder;
        private readonly KafkaChannelFactory _parent;
        private readonly string _topic;
        private readonly ProducerBuilder<byte[], byte[]> _producerBuilder;
        private static readonly Regex s_topicNameRegex =
            new(@"^[a-zA-Z0-9\.\-_]{1,255}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
        private IProducer<byte[], byte[]> _producer;

        public KafkaOutputChannel(KafkaChannelFactory factory, EndpointAddress address, Uri via, MessageEncoder encoder,
            KafkaTransportBindingElement transportBindingElement) : base(factory)
        {
            _parent = factory;
            RemoteAddress = address ?? throw new ArgumentNullException(nameof(address));
            Via = via ?? throw new ArgumentNullException(nameof(via));
            _encoder = encoder ?? throw new ArgumentNullException(nameof(encoder));

            if (!string.Equals(address.Uri.Scheme, KafkaConstants.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException();
            }

            string bootstrapServer = address.Uri.Authority;
            _topic = address.Uri.PathAndQuery.Substring(1, address.Uri.PathAndQuery.Length - 1);
            if (string.IsNullOrEmpty(_topic) || !s_topicNameRegex.IsMatch(_topic))
            {
                throw new NotSupportedException(string.Format(SR.InvalidTopicName, _topic));
            }
            ProducerConfig producerConfig = transportBindingElement.Config;
            producerConfig.BootstrapServers = bootstrapServer;

            _producerBuilder = new ProducerBuilder<byte[], byte[]>(producerConfig);
        }

        protected override void OnOpen(TimeSpan timeout)
        {
            _producer = _producerBuilder.Build();
        }

        private Task OnOpenAsync()
        {
            _producer = _producerBuilder.Build();
            return Task.CompletedTask;
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return OnOpenAsync().ToApm(callback, state);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            result.ToApmEnd();
        }

        protected override void OnAbort()
        {
            OnClose(DefaultCloseTimeout);
        }

        protected override void OnClose(TimeSpan timeout)
        {
            CancellationTokenSource cts = new(timeout);
            try
            {
                _producer.Flush(cts.Token);
            }
            finally
            {
                cts.Dispose();
                _producer.Dispose();
            }
        }

        private Task OnCloseAsync(TimeSpan timeout)
        {
            OnClose(timeout);
            return Task.CompletedTask;
        }

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return OnCloseAsync(timeout).ToApm(callback, state);
        }

        protected override void OnEndClose(IAsyncResult result)
        {
            result.ToApmEnd();
        }

        private ArraySegment<byte> EncodeMessage(Message message)
        {
            try
            {
                RemoteAddress.ApplyTo(message);
                return _encoder.WriteMessage(message, int.MaxValue, _parent.BufferManager);
            }
            finally
            {
                message.Close();
            }
        }

        private async Task SendAsync(Message message, TimeSpan timeout)
        {
            Message<byte[], byte[]> kafkaMessage = new();
            ApplyKafkaMessageProperty(message, kafkaMessage);
            ArraySegment<byte> messageBuffer = EncodeMessage(message);
            CancellationTokenSource cts = new(timeout);
            try
            {
                kafkaMessage.Value = new Span<byte>(messageBuffer.Array, messageBuffer.Offset, messageBuffer.Count).ToArray();
                await _producer.ProduceAsync(_topic, kafkaMessage, cts.Token);
            }
            catch (ProduceException<byte[], byte[]> produceException)
            {
                throw KafkaChannelHelpers.ConvertProduceException(produceException);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(string.Format(SR.KafkaSendTimeoutExceeded, timeout));
            }
            finally
            {
                cts.Dispose();
                _parent.BufferManager.ReturnBuffer(messageBuffer.Array);
            }
        }

        private static void ApplyKafkaMessageProperty(Message message, Message<byte[], byte[]> kafkaMessage)
        {
            KafkaMessageProperty kafkaMessageProperty =
                message.Properties.TryGetValue(KafkaMessageProperty.Name, out object value) &&
                value is KafkaMessageProperty property
                    ? property
                    : new KafkaMessageProperty();
            kafkaMessage.Headers = new();
            foreach (KafkaMessageHeader kafkaMessageHeader in kafkaMessageProperty.Headers)
            {
                kafkaMessage.Headers.Add(kafkaMessageHeader.Key, kafkaMessageHeader.Value);
            }

            kafkaMessage.Key = kafkaMessageProperty.PartitionKey;
        }

        public void Send(Message message) => Send(message, DefaultSendTimeout);

        public void Send(Message message, TimeSpan timeout)
        {
            SendAsync(message, timeout).GetAwaiter().GetResult();
        }

        public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
        {
            return SendAsync(message, DefaultSendTimeout).ToApm(callback, state);
        }

        public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            return BeginSend(message, callback, state);
        }

        public void EndSend(IAsyncResult result)
        {
            result.ToApmEnd();
        }

        public EndpointAddress RemoteAddress { get; }

        public Uri Via { get; }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IOutputChannel))
            {
                return (T)(object)this;
            }

            T messageEncoderProperty = _encoder.GetProperty<T>();
            if (messageEncoderProperty != null)
            {
                return messageEncoderProperty;
            }

            return base.GetProperty<T>();
        }
    }
}

