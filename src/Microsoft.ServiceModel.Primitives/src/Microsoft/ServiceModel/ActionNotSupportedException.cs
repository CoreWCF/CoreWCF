using System;
using System.Runtime.Serialization;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel
{
    //[Serializable]
    public class ActionNotSupportedException : CommunicationException
    {
        public ActionNotSupportedException() { }
        public ActionNotSupportedException(string message) : base(message) { }
        public ActionNotSupportedException(string message, Exception innerException) : base(message, innerException) { }
       // protected ActionNotSupportedException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        internal Message ProvideFault(MessageVersion messageVersion)
        {
            Fx.Assert(messageVersion.Addressing != AddressingVersion.None, "");
            FaultCode code = FaultCode.CreateSenderFaultCode(AddressingStrings.ActionNotSupported, messageVersion.Addressing.Namespace);
            string reason = Message;
            return Microsoft.ServiceModel.Channels.Message.CreateMessage(
               messageVersion, code, reason, messageVersion.Addressing.FaultAction);
        }
    }
}