// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
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