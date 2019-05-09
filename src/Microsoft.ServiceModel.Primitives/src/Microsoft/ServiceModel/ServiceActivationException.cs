using System;
using System.Runtime.Serialization;

namespace Microsoft.ServiceModel
{
    [Serializable]
    public class ServiceActivationException : CommunicationException
    {
        public ServiceActivationException() { }
        public ServiceActivationException(string message) : base(message) { }
        public ServiceActivationException(string message, Exception innerException) : base(message, innerException) { }
        protected ServiceActivationException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
