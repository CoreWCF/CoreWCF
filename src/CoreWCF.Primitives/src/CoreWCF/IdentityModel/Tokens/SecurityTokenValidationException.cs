using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace CoreWCF.IdentityModel.Tokens
{
    [Serializable]
    internal class SecurityTokenValidationException : SecurityTokenException
    {
        public SecurityTokenValidationException()
            : base()
        {
        }

        public SecurityTokenValidationException(string message)
            : base(message)
        {
        }

        public SecurityTokenValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected SecurityTokenValidationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
