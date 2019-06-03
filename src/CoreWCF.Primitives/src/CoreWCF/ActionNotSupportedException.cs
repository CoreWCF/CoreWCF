using System;
using System.Runtime.Serialization;
using CoreWCF.Runtime;
using CoreWCF.Channels;

namespace CoreWCF
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
            return CoreWCF.Channels.Message.CreateMessage(
               messageVersion, code, reason, messageVersion.Addressing.FaultAction);
        }
    }
}