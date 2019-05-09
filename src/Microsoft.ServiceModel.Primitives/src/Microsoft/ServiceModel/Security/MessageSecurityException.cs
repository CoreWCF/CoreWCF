using System;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Security
{
    [Serializable]
    public class MessageSecurityException : CommunicationException
    {
        MessageFault fault;
        bool isReplay = false;

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