// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Claims
{
    /// <summary>
    /// The authentication information that an authority asserted when creating a token for a subject.
    /// </summary>
    public class AuthenticationInformation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticationInformation"/> class.
        /// </summary>
        public AuthenticationInformation()
        {
            AuthorizationContexts = new Collection<AuthenticationContext>();
        }

        /// <summary>
        /// Gets or sets the address of the authority that created the token.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Gets the <see cref="AuthorizationContext"/> used by the authenticating authority when issuing tokens.
        /// </summary>
        public Collection<AuthenticationContext> AuthorizationContexts { get; }

        /// <summary>
        /// Gets or sets the DNS name of the authority that created the token.
        /// </summary>
        public string DnsName { get; set; }

        /// <summary>
        /// Gets or sets the time that the session referred to in the session index MUST be considered ended.
        /// </summary>
        public DateTime? NotOnOrAfter { get; set; }

        /// <summary>
        /// Gets or sets the session index that describes the session between the authority and the client.
        /// </summary>
        public string Session { get; set; }
    }
}
