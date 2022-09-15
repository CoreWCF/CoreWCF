// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common;
using CoreWCF.Queue.CoreWCF.Queue;

namespace CoreWCF.Channels
{
    public sealed class MsmqTransportBindingElement : MsmqBindingElementBase
    {
        private int _maxPoolSize = MsmqDefaults.MaxPoolSize;
        private bool _useActiveDirectory = MsmqDefaults.UseActiveDirectory;

        public MsmqTransportBindingElement() { }

        private MsmqTransportBindingElement(MsmqTransportBindingElement elementToBeCloned)
            : base(elementToBeCloned)
        {
            _useActiveDirectory = elementToBeCloned._useActiveDirectory;
            _maxPoolSize = elementToBeCloned._maxPoolSize;
        }


        public int MaxPoolSize
        {
            get
            {
                return _maxPoolSize;
            }
            set
            {
                if (value < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ArgumentOutOfRangeException("value", value, SR.MsmqNonNegativeArgumentExpected));
                }
                _maxPoolSize = value;
            }
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
                _useActiveDirectory = value;
            }
        }

        public override QueueTransportPump BuildQueueTransportPump(BindingContext context)
        {
            IQueueTransport queueTransport = CreateMyQueueTransport(context);  
            return QueueTransportPump.CreateDefaultPump(queueTransport);
        }

        private IQueueTransport CreateMyQueueTransport(BindingContext context)
        {
            var _queueOptions = context.BindingParameters.Find<QueueOptions>();
            var serviceDispatcher = context.BindingParameters.Find<IServiceDispatcher>();
            //TODO : add queue before exists
            return new MsmqNetcoreTransport(_queueOptions, serviceDispatcher);
        }

        public override BindingElement Clone()
        {
            return new MsmqTransportBindingElement(this);
        }
    }
}
