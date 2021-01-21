// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Security
{
    [Serializable]
    public class MessageSecurityException : CommunicationException
    {
        private MessageFault fault;
        private bool isReplay = false;

        public MessageSecurityException()
            : base()
        {
        }

        public MessageSecurityException(string message)
            : base(message)
        {
        }

        public MessageSecurityException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected MessageSecurityException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }

        internal MessageSecurityException(string message, Exception innerException, MessageFault fault)
            : base(message, innerException)
        {
            this.fault = fault;
        }

        internal MessageSecurityException(string message, bool isReplay)
            : base(message)
        {
            this.isReplay = isReplay;
        }

        internal bool ReplayDetected
        {
            get
            {
                return isReplay;
            }
        }

        internal MessageFault Fault
        {
            get { return fault; }
        }
    }

}