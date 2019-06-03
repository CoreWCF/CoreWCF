using System;

namespace CoreWCF
{
    //[Serializable]
    public class EndpointNotFoundException : CommunicationException
    {
        public EndpointNotFoundException() { }
        public EndpointNotFoundException(string message) : base(message) { }
        public EndpointNotFoundException(string message, Exception innerException) : base(message, innerException) { }
        //protected EndpointNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}