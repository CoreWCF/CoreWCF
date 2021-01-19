using System;

namespace CoreWCF.IdentityModel
{
    [Serializable]
    public class SecurityMessageSerializationException : SystemException
    {
        public SecurityMessageSerializationException()
            : base()
        {
        }

        public SecurityMessageSerializationException(String message)
            : base(message)
        {
        }

        public SecurityMessageSerializationException(String message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected SecurityMessageSerializationException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
        }

    }
}
