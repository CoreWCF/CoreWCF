using System;

namespace Microsoft.ServiceModel
{
    //[Serializable]
    internal class ChannelTerminatedException : CommunicationException
    {
        public ChannelTerminatedException() { }
        public ChannelTerminatedException(string message) : base(message) { }
        public ChannelTerminatedException(string message, Exception innerException) : base(message, innerException) { }
        //protected ChannelTerminatedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}