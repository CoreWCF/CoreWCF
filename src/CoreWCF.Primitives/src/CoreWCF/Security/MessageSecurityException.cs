// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;

namespace CoreWCF.Security
{
    [Serializable]
    public class MessageSecurityException : CommunicationException
    {
        private readonly MessageFault _fault;
        private readonly bool _isReplay = false;

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
            _fault = fault;
        }

        internal MessageSecurityException(string message, bool isReplay)
            : base(message)
        {
            _isReplay = isReplay;
        }

        internal bool ReplayDetected
        {
            get
            {
                return _isReplay;
            }
        }

        internal MessageFault Fault
        {
            get { return _fault; }
        }
    }
}