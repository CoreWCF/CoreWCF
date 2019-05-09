using System;

namespace Microsoft.ServiceModel
{
    public class CommunicationObjectFaultedException : CommunicationException
    {
        public CommunicationObjectFaultedException() { }
        public CommunicationObjectFaultedException(string message) : base(message) { }
        public CommunicationObjectFaultedException(string message, Exception innerException) : base(message, innerException) { }
        //protected CommunicationObjectFaultedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}