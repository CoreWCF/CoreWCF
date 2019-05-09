using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Microsoft.ServiceModel.Security
{
    [Serializable]
    public class SecurityNegotiationException : CommunicationException
    {
        public SecurityNegotiationException()
            : base()
        {
        }

        public SecurityNegotiationException(string message)
            : base(message)
        {
        }

        public SecurityNegotiationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected SecurityNegotiationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
