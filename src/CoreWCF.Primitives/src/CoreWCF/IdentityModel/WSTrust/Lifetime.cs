// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.IdentityModel.Protocols.WSTrust
{
    /// <summary>
    /// Used in the RequestSecurityToken or RequestSecurityTokenResponse to indicated the desired or 
    /// required lifetime of a token. Everything here is stored in Utc format.
    /// </summary>
    public class Lifetime
    {
        /// <summary>
        /// Instantiates a LifeTime object with token creation and expiration time in Utc.
        /// </summary>
        /// <param name="created">Token creation time in Utc.</param>
        /// <param name="expires">Token expiration time in Utc.</param>
        /// <exception cref="ArgumentException">When the given expiration time is 
        /// before the given creation time.</exception>
        public Lifetime(DateTime created, DateTime expires)
            : this((DateTime?)created, (DateTime?)expires)
        {
        }

        /// <summary>
        /// Instantiates a LifeTime object with token creation and expiration time in Utc.
        /// </summary>
        /// <param name="created">Token creation time in Utc.</param>
        /// <param name="expires">Token expiration time in Utc.</param>
        /// <exception cref="ArgumentException">When the given expiration time is 
        /// before the given creation time.</exception>
        public Lifetime(DateTime? created, DateTime? expires)
        {
            if (created != null && expires != null && expires.Value <= created.Value)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.ID2000)));

            Created = DateTimeUtil.ToUniversalTime(created);
            Expires = DateTimeUtil.ToUniversalTime(expires);
        }

        /// <summary>
        /// Gets the token creation time in UTC time.
        /// </summary>
        public DateTime? Created { get; set; }

        /// <summary>
        /// Gets the token expiration time in UTC time.
        /// </summary>
        public DateTime? Expires { get; set; }
    }
}
