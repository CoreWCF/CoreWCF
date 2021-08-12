// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
