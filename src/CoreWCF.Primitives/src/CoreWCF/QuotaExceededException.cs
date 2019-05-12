using System;
using System.Runtime.Serialization;

namespace CoreWCF
{
    public class QuotaExceededException : Exception //SystemException
    {
        public QuotaExceededException()
            : base()
        {
        }

        public QuotaExceededException(string message)
            : base(message)
        {
        }

        public QuotaExceededException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}