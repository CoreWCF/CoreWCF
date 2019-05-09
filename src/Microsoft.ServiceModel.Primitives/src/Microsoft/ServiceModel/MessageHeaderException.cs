using System;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel
{
    //[Serializable]
    public class MessageHeaderException : ProtocolException
    {
        //[NonSerialized]
        string headerName;
        //[NonSerialized]
        string headerNamespace;
        //[NonSerialized]
        bool isDuplicate;

        public MessageHeaderException(string message)
            : this(message, null, null)
        {
        }
        public MessageHeaderException(string message, bool isDuplicate)
            : this(message, null, null)
        {
        }
        public MessageHeaderException(string message, Exception innerException)
            : this(message, null, null, innerException)
        {
        }
        public MessageHeaderException(string message, string headerName, string ns)
            : this(message, headerName, ns, null)
        {
        }
        public MessageHeaderException(string message, string headerName, string ns, bool isDuplicate)
            : this(message, headerName, ns, isDuplicate, null)
        {
        }
        public MessageHeaderException(string message, string headerName, string ns, Exception innerException)
            : this(message, headerName, ns, false, innerException)
        {
        }
        public MessageHeaderException(string message, string headerName, string ns, bool isDuplicate, Exception innerException)
            : base(message, innerException)
        {
            this.headerName = headerName;
            headerNamespace = ns;
            this.isDuplicate = isDuplicate;
        }

        public string HeaderName { get { return headerName; } }

        public string HeaderNamespace { get { return headerNamespace; } }

        // IsDuplicate==true means there was more than one; IsDuplicate==false means there were zero
        public bool IsDuplicate { get { return isDuplicate; } }

        internal Message ProvideFault(MessageVersion messageVersion)
        {
            Fx.Assert(messageVersion.Addressing == AddressingVersion.WSAddressing10, "");
            WSAddressing10ProblemHeaderQNameFault phf = new WSAddressing10ProblemHeaderQNameFault(this);
            Message message = Microsoft.ServiceModel.Channels.Message.CreateMessage(messageVersion, phf, AddressingVersion.WSAddressing10.FaultAction);
            phf.AddHeaders(message.Headers);
            return message;
        }

        // for serialization
        public MessageHeaderException() { }
        //protected MessageHeaderException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

}