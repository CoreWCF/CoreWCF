using System;
using System.Runtime.Serialization;

namespace CoreWCF
{
    [Serializable]
    public class ServerTooBusyException : CommunicationException
    {
        public ServerTooBusyException() { }
        public ServerTooBusyException(string message) : base(message) { }
        public ServerTooBusyException(string message, Exception innerException) : base(message, innerException) { }
        protected ServerTooBusyException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}