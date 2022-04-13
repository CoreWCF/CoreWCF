// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// This class is used to specify the context of an authentication event.
    /// </summary>
    public class AuthenticationContext
    {
        /// <summary>
        /// Creates an instance of AuthenticationContext.
        /// </summary>
        public AuthenticationContext()
        {
            Authorities = new Collection<string>();
        }

        /// <summary>
        /// The collection of authorities for resolving an authentication event.
        /// </summary>
        public Collection<string> Authorities { get; }

        /// <summary>
        /// The context class for resolving an authentication event.
        /// </summary>
        public string ContextClass { get; set; }

        /// <summary>
        /// The context declaration for resolving an authentication event.
        /// </summary>
        public string ContextDeclaration { get; set; }
    }
}
