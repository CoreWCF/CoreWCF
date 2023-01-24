// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common;
using CoreWCF.Queue.Common.Configuration;
using Azure.Storage.Queues;

namespace CoreWCF.Channels
{
    public class AzureQueueStorageTransportBindingElement : QueueBaseTransportBindingElement
    {
        /// <summary>
        /// Creates a new instance of the AzureQueueStorageTransportBindingElement Class using the default protocol.
        /// </summary>
        public AzureQueueStorageTransportBindingElement()
        {
        }

        public override BindingElement Clone()
        {
            return new AzureQueueStorageTransportBindingElement();
        }


        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(context));
            }

            if (typeof(T) == typeof(ISecurityCapabilities))
            {
                return null;
            }

            return base.GetProperty<T>(context);
        }

        public override QueueTransportPump BuildQueueTransportPump(BindingContext context)
        {
            IQueueTransport queueTransport = CreateMyQueueTransport(context);
            return QueueTransportPump.CreateDefaultPump(queueTransport);
        }

        private IQueueTransport CreateMyQueueTransport(BindingContext context)
        {
            var serviceDispatcher = context.BindingParameters.Find<IServiceDispatcher>();
            var serviceProvider = context.BindingParameters.Find<IServiceProvider>();
            CreateQueue(serviceDispatcher.BaseAddress);
            return new AzureQueueStorageQueueTransport(serviceDispatcher, serviceProvider);
        }

        private void CreateQueue(Uri localAddress)
        {
            var queueName = AzureQueueStorageQueueNameConverter.GetAzureQueueStorageQueueName(localAddress);
            //if (!_.Exists(queueName))
            {
              //  MessageQueue.Create(queueName);
            }
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

    }
}
