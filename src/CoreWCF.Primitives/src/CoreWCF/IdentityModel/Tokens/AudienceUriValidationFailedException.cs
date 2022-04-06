// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Serialization;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Throw this exception a received Security token failed Audience Uri validation.
    /// </summary>
    [Serializable]
    public class AudienceUriValidationFailedException : SecurityTokenValidationException
    {
        /// <summary>
        /// Initializes a new instance of  <see cref="AudienceUriValidationFailedException"/>
        /// </summary>
        public AudienceUriValidationFailedException()
            : base( SR.Format( SR.ID4183 ) )
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="AudienceUriValidationFailedException"/>
        /// </summary>
        public AudienceUriValidationFailedException( string message )
            : base( message )
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="AudienceUriValidationFailedException"/>
        /// </summary>
        public AudienceUriValidationFailedException( string message, Exception inner )
            : base( message, inner )
        {
        }

        /// <summary>
        /// Initializes a new instance of  <see cref="AudienceUriValidationFailedException"/>
        /// </summary>
        protected AudienceUriValidationFailedException( SerializationInfo info, StreamingContext context )
            : base( info, context )
        {
        }
    }
}
