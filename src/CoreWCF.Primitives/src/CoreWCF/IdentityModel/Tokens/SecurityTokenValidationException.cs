// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace CoreWCF.IdentityModel.Tokens
{
    [Serializable]
    public class SecurityTokenValidationException : SecurityTokenException
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
