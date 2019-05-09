using System;
using System.Runtime.Serialization;

namespace Microsoft.ServiceModel
{
    [Serializable]
    public class AddressAlreadyInUseException : CommunicationException
    {
        public AddressAlreadyInUseException() { }
        public AddressAlreadyInUseException(string message) : base(message) { }
        public AddressAlreadyInUseException(string message, Exception innerException) : base(message, innerException) { }
        protected AddressAlreadyInUseException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
