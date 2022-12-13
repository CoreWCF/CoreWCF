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
        private readonly ProducerBuilder<Null, byte[]> _producerBuilder;
        private static readonly Regex s_topicNameRegex =
            new(@"^[a-zA-Z0-9\.\-_]{1,255}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
        private IProducer<Null, byte[]> _producer;

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
                throw new NotSupportedException($"The specified topic name '{_topic}' is not valid");
            }
            ProducerConfig producerConfig = transportBindingElement.Config;
            producerConfig.BootstrapServers = bootstrapServer;

            _producerBuilder = new ProducerBuilder<Null, byte[]>(producerConfig);
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

        private async Task SendAsync(Message message, CancellationToken token)
        {
            ArraySegment<byte> messageBuffer = EncodeMessage(message);
            try
            {
                byte[] bytes = new Span<byte>(messageBuffer.Array, messageBuffer.Offset, messageBuffer.Count).ToArray();
                await _producer.ProduceAsync(_topic, new Message<Null, byte[]> { Value = bytes }, token);
            }
            catch (ProduceException<Null, byte[]> produceException)
            {
                throw KafkaChannelHelpers.ConvertProduceException(produceException);
            }
            finally
            {
                _parent.BufferManager.ReturnBuffer(messageBuffer.Array);
            }
        }

        public void Send(Message message) => Send(message, DefaultSendTimeout);

        private void ProduceSynchronously(byte[] bytes, TimeSpan timeout)
        {
            // the .net synchronous Consumer.Produce method produces asynchronously and uses a callback to signal message is sent.
            // Usual production pattern is to call producer.Produce multiple times and then producer.Flush
            // So here the throughput will be significantly impacted but will better fit CoreWCF model
            ManualResetEventSlim mre = new();
            _producer.Produce(_topic, new Message<Null, byte[]> { Value = bytes }, report =>
            {
                try
                {
                    if (report.Error.IsError)
                    {
                        throw KafkaChannelHelpers.ConvertError(report.Error);
                    }
                }
                finally
                {
                    mre.Set();
                }
            });
            mre.Wait(timeout);
        }

        public void Send(Message message, TimeSpan timeout)
        {
            ArraySegment<byte> messageBuffer = EncodeMessage(message);
            try
            {
                byte[] bytes = new Span<byte>(messageBuffer.Array, messageBuffer.Offset, messageBuffer.Count).ToArray();
                ProduceSynchronously(bytes, timeout);
            }
            catch (ProduceException<Null, byte[]> produceException)
            {
                throw KafkaChannelHelpers.ConvertProduceException(produceException);
            }
            finally
            {
                _parent.BufferManager.ReturnBuffer(messageBuffer.Array);
            }
        }

        public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
        {
            return SendAsync(message, CancellationToken.None).ToApm(callback, state);
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
