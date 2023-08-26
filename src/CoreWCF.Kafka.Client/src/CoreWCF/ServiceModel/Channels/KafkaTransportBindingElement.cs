// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using Confluent.Kafka;

namespace CoreWCF.ServiceModel.Channels
{
    public partial class KafkaTransportBindingElement : TransportBindingElement
    {
        public override string Scheme => KafkaConstants.Scheme;

        internal ProducerConfig Config { get; } = new();

        public KafkaTransportBindingElement()
        {

        }

        private KafkaTransportBindingElement(KafkaTransportBindingElement other)
            : base(other)
        {
            Config = other.Config;
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return context.GetInnerProperty<T>();
        }

        public override BindingElement Clone()
        {
            return new KafkaTransportBindingElement(this);
        }

        public override bool CanBuildChannelFactory<TChannel>(BindingContext context) => typeof(TChannel) == typeof(IOutputChannel);

        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            return (IChannelFactory<TChannel>)new KafkaChannelFactory(this, context);
        }
    }
}
