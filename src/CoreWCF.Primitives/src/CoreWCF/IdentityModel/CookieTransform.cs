// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.IdentityModel
{
    /// <summary>
    /// Applies a reversible data transform to cookie data. 
    /// </summary>
    public abstract class CookieTransform
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CookieTransform"/> class.
        /// </summary>
        protected CookieTransform() { }

        /// <summary>
        /// Reverses the transform.
        /// </summary>
        /// <param name="encoded">The encoded form of the cookie.</param>
        /// <returns>The decoded byte array.</returns>
        public abstract byte[] Decode( byte[] encoded );

        /// <summary>
        /// Applies the transform.
        /// </summary>
        /// <param name="value">The byte array to be encoded.</param>
        /// <returns>The encoded cookie.</returns>
        public abstract byte[] Encode( byte[] value );
    }
}
