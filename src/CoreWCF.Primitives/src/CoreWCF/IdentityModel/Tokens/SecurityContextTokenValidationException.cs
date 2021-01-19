using System;
using System.Runtime.Serialization;
namespace CoreWCF.IdentityModel.Tokens
{
    [Serializable]
    class SecurityContextTokenValidationException : SecurityTokenValidationException
    {
        public SecurityContextTokenValidationException()
            : base()
        {
        }

        public SecurityContextTokenValidationException(string message)
            : base(message)
        {
        }

        public SecurityContextTokenValidationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected SecurityContextTokenValidationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
