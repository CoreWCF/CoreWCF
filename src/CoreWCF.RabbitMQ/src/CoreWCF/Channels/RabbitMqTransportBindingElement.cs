// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Queue.Common;
using CoreWCF.Queue.Common.Configuration;
using CoreWCF.RabbitMQ.CoreWCF.Channels;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace CoreWCF.Channels
{
    public class RabbitMqTransportBindingElement : QueueBaseTransportBindingElement
    {
        /// <summary>
        /// Creates a new instance of the RabbitMQTransportBindingElement Class using the default protocol.
        /// </summary>
        public RabbitMqTransportBindingElement()
        {
            MaxReceivedMessageSize = RabbitMqBinding.DefaultMaxMessageSize;
        }

        private RabbitMqTransportBindingElement(RabbitMqTransportBindingElement other)
        {
            BrokerProtocol = other.BrokerProtocol;
            UsernameConfigKey = other.UsernameConfigKey;
            PasswordConfigKey = other.UsernameConfigKey;
            VirtualHost = other.VirtualHost;
            MaxReceivedMessageSize = other.MaxReceivedMessageSize;
        }


        public override BindingElement Clone()
        {
            return new RabbitMqTransportBindingElement(this);
        }


        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("context");
            }

            if (typeof(T) == typeof(ISecurityCapabilities))
            {
                return null;
            }

            return base.GetProperty<T>(context);
        }

        public override QueueTransportPump BuildQueueTransportPump(BindingContext context)
        {
            IServiceProvider serviceProvider = context.BindingParameters.Find<IServiceProvider>();
            return serviceProvider.GetRequiredService<RabbitMqTransportPump>();
        }

        /// <summary>
        /// Gets the scheme used by the binding, soap.amqp
        /// </summary>
        public override string Scheme
        {
            get { return CurrentVersion.Scheme; }
        }


        /// <summary>
        /// The largest receivable encoded message
        /// </summary>
        public override long MaxReceivedMessageSize { get; set; }

        /// <summary>
        /// The username  to use when authenticating with the broker
        /// </summary>
        internal string UsernameConfigKey { get; set; }

        /// <summary>
        /// Password to use when authenticating with the broker
        /// </summary>
        internal string PasswordConfigKey { get; set; }

        /// <summary>
        /// Specifies the broker virtual host
        /// </summary>
        internal string VirtualHost { get; set; }

        /// <summary>
        /// Specifies the version of the AMQP protocol that should be used to
        /// communicate with the broker
        /// </summary>
        public IProtocol BrokerProtocol { get; set; }

    }
}
