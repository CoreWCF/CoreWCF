// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.ServiceModel.Channels
{
    internal class KafkaChannelFactory : ChannelFactoryBase<IOutputChannel>
    {
        private readonly BufferManager _bufferManager;
        private readonly KafkaTransportBindingElement _transportTransportBindingElement;

        public KafkaChannelFactory(KafkaTransportBindingElement transportBindingElement,
            BindingContext context)
            : base(context.Binding)
        {
            _transportTransportBindingElement = transportBindingElement;
            _bufferManager = BufferManager.CreateBufferManager(transportBindingElement.MaxBufferPoolSize, int.MaxValue);

            MessageEncoderFactory = context.BindingParameters.Find<MessageEncodingBindingElement>()?.CreateMessageEncoderFactory()
                ?? KafkaConstants.DefaultMessageEncoderFactory;
        }

        public BufferManager BufferManager => _bufferManager;

        public MessageEncoderFactory MessageEncoderFactory { get; }

        private Task OnOpenAsync => Task.CompletedTask;

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return OnOpenAsync.ToApm(callback, state);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            result.ToApmEnd();
        }

        protected override void OnOpen(TimeSpan timeout)
        {

        }

        protected override IOutputChannel OnCreateChannel(EndpointAddress address, Uri via)
        {
            return new KafkaOutputChannel(this, address, via, MessageEncoderFactory.Encoder, _transportTransportBindingElement);
        }

        protected override void OnClosed()
        {
            base.OnClosed();
            _bufferManager.Clear();
        }
    }
}
