// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Throw this exception when a received Security Token has expiration time in the past.
    /// </summary>
    [Serializable]
    public class SecurityTokenExpiredException : SecurityTokenValidationException
    {
        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenExpiredException"/>
        /// </summary>
        public SecurityTokenExpiredException()
            : base(SR.Format(SR.ID4181))
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenExpiredException"/>
        /// </summary>
        public SecurityTokenExpiredException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenExpiredException"/>
        /// </summary>
        public SecurityTokenExpiredException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenExpiredException"/>
        /// </summary>
        protected SecurityTokenExpiredException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
