// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;

namespace CoreWCF.IdentityModel.Tokens.Saml
{
    /// <summary>
    /// Represents the SecurityTokenReference property of X509Data as per:  https://www.w3.org/TR/2001/PR-xmldsig-core-20010820/#sec-X509Data
    /// </summary>
    public class SecurityTokenReference
    {
        /// <summary>
        /// Creates an IssuerSerial using the specified IssuerName and SerialNumber.
        /// </summary>
        public SecurityTokenReference(SecurityKeyIdentifier securityKeyIdentifier)
        {
            SecurityKeyIdentifier = securityKeyIdentifier;
        }

        /// <summary>
        /// Gets the SecurityKeyIdentifier
        /// </summary>
        public SecurityKeyIdentifier SecurityKeyIdentifier { get; }

        /// <summary>
        /// Compares two SecurityTokenReference instances.
        /// </summary>
        /// <param name="securityKey"></param>
        /// <returns></returns>
        public bool MatchesKey(Microsoft.IdentityModel.Tokens.SecurityKey securityKey)
        {
            if (securityKey == null)
                return false;

            if (SecurityKeyIdentifier == null)
                return false;

            if (securityKey is Microsoft.IdentityModel.Tokens.X509SecurityKey x509SecurityKey)
            {
                X509SubjectKeyIdentifierExtension skiExtension = x509SecurityKey.Certificate.Extensions["2.5.29.14"] as X509SubjectKeyIdentifierExtension;
                if (skiExtension == null)
                    return false;

                // TODO: skiExtension.SecurityKeyIdentifier.Value is base64 encoded, skiExtension.RawData is not

                return SecurityKeyIdentifier?.Value == Convert.ToBase64String(skiExtension.RawData, 2, skiExtension.RawData.Length - 2);
            }

            return false;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            if (obj is SecurityTokenReference other)
                return SecurityKeyIdentifier?.Value == other.SecurityKeyIdentifier?.Value;

            return false;
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            return SecurityKeyIdentifier?.GetHashCode() ?? 0;
        }
    }
}

