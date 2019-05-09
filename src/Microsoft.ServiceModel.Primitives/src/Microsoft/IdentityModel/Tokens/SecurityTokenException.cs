using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Microsoft.IdentityModel.Tokens
{
    internal class SecurityTokenException : Exception
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
