// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;

namespace CoreWCF.Channels
{ 
    public abstract class MsmqBindingBase : Binding
    {
        internal MsmqBindingElementBase _transport;

        protected MsmqBindingBase()
        { }

        // todo: need to be enabled when we add System.Transactions support
        // [DefaultValue(typeof(System.TimeSpan), MsmqDefaults.ValidityDurationString)]
        //public TimeSpan ValidityDuration
        //{
        //    get { return transport.ValidityDuration; }
        //    set { transport.ValidityDuration = value; }
        //}

        [DefaultValue(MsmqDefaults.CustomDeadLetterQueue)]
        public Uri CustomDeadLetterQueue
        {
            get { return _transport.CustomDeadLetterQueue; }
            set { _transport.CustomDeadLetterQueue = value; }
        }

        [DefaultValue(MsmqDefaults.DeadLetterQueue)]
        public DeadLetterQueue DeadLetterQueue
        {
            get { return _transport.DeadLetterQueue; }
            set { _transport.DeadLetterQueue = value; }
        }
        
        [DefaultValue(MsmqDefaults.ExactlyOnce)]
        public bool ExactlyOnce
        {
            get { return _transport.ExactlyOnce; }
            set { _transport.ExactlyOnce = value; }
        }

        [DefaultValue(TransportDefaults.MaxReceivedMessageSize)]
        public long MaxReceivedMessageSize
        {
            get { return _transport.MaxReceivedMessageSize; }
            set { _transport.MaxReceivedMessageSize = value; }
        }


        [DefaultValue(MsmqDefaults.ReceiveContextEnabled)]
        public bool ReceiveContextEnabled
        {
            get { return _transport.ReceiveContextEnabled; }
            set { _transport.ReceiveContextEnabled = value; }
        }

        // todo: need to be enabled when we add System.Transactions support
        //[DefaultValue(MsmqDefaults.ReceiveErrorHandling)]
        //public ReceiveErrorHandling ReceiveErrorHandling
        //{
        //    get { return transport.ReceiveErrorHandling; }
        //    set { transport.ReceiveErrorHandling = value; }
        //}      

        public override string Scheme { get { return _transport.Scheme; } }   
    }
}
