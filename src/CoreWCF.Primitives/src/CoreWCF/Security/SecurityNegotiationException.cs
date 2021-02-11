// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace CoreWCF.Security
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
