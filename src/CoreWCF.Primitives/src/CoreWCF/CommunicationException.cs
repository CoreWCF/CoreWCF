using System;
using System.Runtime.Serialization;

namespace CoreWCF
{
    [Serializable]
    public class CommunicationException : Exception //SystemException
    {
        public CommunicationException() { }
        public CommunicationException(string message) : base(message) { }
        public CommunicationException(string message, Exception innerException) : base(message, innerException) { }
        protected CommunicationException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}