// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Throw this exception when a received Security Token has been replayed.
    /// </summary>
    [Serializable]
    public class SecurityTokenReplayDetectedException : SecurityTokenValidationException
    {
        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenReplayDetectedException"/>
        /// </summary>
        public SecurityTokenReplayDetectedException()
            : base(SR.Format(SR.ID1070))
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenReplayDetectedException"/>
        /// </summary>
        public SecurityTokenReplayDetectedException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenReplayDetectedException"/>
        /// </summary>
        public SecurityTokenReplayDetectedException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="SecurityTokenReplayDetectedException"/>
        /// </summary>
        protected SecurityTokenReplayDetectedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
