﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common;
using MSMQ.Messaging;

namespace CoreWCF.Channels
{
    public sealed class MsmqTransportBindingElement : MsmqBindingElementBase
    {
        private bool _useActiveDirectory = MsmqDefaults.UseActiveDirectory;

        public MsmqTransportBindingElement() { }

        private MsmqTransportBindingElement(MsmqTransportBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _useActiveDirectory = elementToBeCloned._useActiveDirectory;
        }

        public override string Scheme
        {
            get
            {
                return "net.msmq";
            }
        }

        public bool UseActiveDirectory
        {
            get
            {
                return _useActiveDirectory;
            }
            set
            {
                throw new NotImplementedException();
            }
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
            return new MsmqQueueTransport(serviceDispatcher, serviceProvider);
        }

        public override BindingElement Clone()
        {
            return new MsmqTransportBindingElement(this);
        }

        private void CreateQueue(Uri localAddress)
        {
            var queueName = MsmqQueueNameConverter.GetMsmqFormatQueueName(localAddress);
            if (!MessageQueue.Exists(queueName))
            {
                MessageQueue.Create(queueName);
            }
        }
    }
}
