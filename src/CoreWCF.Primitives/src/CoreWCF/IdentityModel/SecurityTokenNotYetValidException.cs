// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Throw this exception when a received Security token has an effective time 
    /// in the future.
    /// </summary>
    [Serializable]
    public class SecurityTokenNotYetValidException : SecurityTokenValidationException
    {
        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenNotYetValidException"/>
        /// </summary>
        public SecurityTokenNotYetValidException()
            : base(SR.Format(SR.ID4182))
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenNotYetValidException"/>
        /// </summary>
        public SecurityTokenNotYetValidException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenNotYetValidException"/>
        /// </summary>
        public SecurityTokenNotYetValidException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenNotYetValidException"/>
        /// </summary>
        protected SecurityTokenNotYetValidException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
