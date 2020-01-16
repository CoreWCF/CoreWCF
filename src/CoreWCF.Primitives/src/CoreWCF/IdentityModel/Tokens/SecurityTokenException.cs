using System;
using System.Runtime.Serialization;

namespace CoreWCF.IdentityModel.Tokens
{
    public class SecurityTokenException : Exception
    {
        public SecurityTokenException()
            : base()
        {
        }

        public SecurityTokenException(string message)
            : base(message)
        {
        }

        public SecurityTokenException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected SecurityTokenException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
