using System;

namespace Microsoft.ServiceModel
{
    public class CommunicationObjectAbortedException : CommunicationException
    {
        public CommunicationObjectAbortedException() { }
        public CommunicationObjectAbortedException(string message) : base(message) { }
        public CommunicationObjectAbortedException(string message, Exception innerException) : base(message, innerException) { }
        //protected CommunicationObjectAbortedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}