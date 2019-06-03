using CoreWCF.Runtime;
using CoreWCF.Channels;
using System;
using System.Runtime.Serialization;

namespace CoreWCF
{
    [Serializable]
    internal class ActionMismatchAddressingException : ProtocolException
    {
        string httpActionHeader;
        string soapActionHeader;

        public ActionMismatchAddressingException(string message, string soapActionHeader, string httpActionHeader)
            : base(message)
        {
            this.httpActionHeader = httpActionHeader;
            this.soapActionHeader = soapActionHeader;
        }

        protected ActionMismatchAddressingException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public string HttpActionHeader
        {
            get
            {
                return httpActionHeader;
            }
        }

        public string SoapActionHeader
        {
            get
            {
                return soapActionHeader;
            }
        }

        internal Message ProvideFault(MessageVersion messageVersion)
        {
            Fx.Assert(messageVersion.Addressing == AddressingVersion.WSAddressing10, "");
            WSAddressing10ProblemHeaderQNameFault phf = new WSAddressing10ProblemHeaderQNameFault(this);
            Message message = CoreWCF.Channels.Message.CreateMessage(messageVersion, phf, messageVersion.Addressing.FaultAction());
            phf.AddHeaders(message.Headers);
            return message;
        }
    }
}
