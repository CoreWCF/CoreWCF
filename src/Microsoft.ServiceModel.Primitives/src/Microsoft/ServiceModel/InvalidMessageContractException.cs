using System;

namespace Microsoft.ServiceModel
{
    //[Serializable]
    internal class InvalidMessageContractException : Exception //SystemException
    {
        public InvalidMessageContractException()
            : base()
        {
        }

        public InvalidMessageContractException(string message)
            : base(message)
        {
        }

        public InvalidMessageContractException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        //protected InvalidMessageContractException(SerializationInfo info, StreamingContext context)
        //    : base(info, context)
        //{
        //}
    }
}