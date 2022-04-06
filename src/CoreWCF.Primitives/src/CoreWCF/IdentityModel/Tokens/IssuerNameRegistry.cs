// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Claims;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Interface that defines the name service that returns that issuer name
    /// of a given token as string. 
    /// </summary>
    public abstract class IssuerNameRegistry //: ICustomIdentityConfiguration
    {
        /// <summary>
        /// When implemented in the derived class, the method returns the issuer name of the given 
        /// SecurityToken's issuer.  Implementations must return a non-null and non-empty string to
        /// identify a recognized issuer, or a null string to identify an unrecognized issuer.
        /// </summary>
        /// <param name="securityToken">The SecurityToken whose name is requested.</param>
        /// <returns>Issuer name as a string.</returns>
        public abstract string GetIssuerName( SecurityToken securityToken );

        /// <summary>
        /// When implemented in the derived class the method returns the issuer name 
        /// of the given SecurityToken's issuer. The requested issuer name may be considered
        /// in determining the issuer's name.
        /// </summary>
        /// <param name="securityToken">The SecurityToken whose name is requested.</param>
        /// <param name="requestedIssuerName">Input to determine the issuer name</param>
        /// <remarks>The default implementation ignores the requestedIsserName parameter and simply calls the 
        /// GetIssuerName( SecurityToken securityToken ) method</remarks>
        /// <returns>Issuer name as a string.</returns>
        public virtual string GetIssuerName( SecurityToken securityToken, string requestedIssuerName )
        {
            return GetIssuerName( securityToken );
        }

        /// <summary>
        /// This function returns the default issuer name to be used for Windows claims.
        /// </summary>
        /// <returns>Issuer name as a string.</returns>
        public virtual string GetWindowsIssuerName()
        {
            return ClaimsIdentity.DefaultIssuer;
        }
    }
}
