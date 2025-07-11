// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.IdentityModel.Tokens.Saml
{
    /// <summary>
    /// Represents the KeyIdentifier of X509Data as per:  TODO - need url to STRTransform
    /// </summary>
    public class SecurityKeyIdentifier
    {
        /// <summary>
        /// Creates an SecurityKeyIdentifier using the specified KeyIdentifierType and EncodingType.
        /// </summary>
        public SecurityKeyIdentifier(string valueType, string encodingType, string value)
        {
            ValueType = valueType;
            EncodingType = encodingType;
            Value = value;
        }

        /// <summary>
        /// Gets the ValueTupe of the SecurityKeyIdentifier
        /// </summary>
        public string ValueType { get; }

        /// <summary>
        /// Gets the EncodingType of the SecurityKeyIdentifier.
        /// </summary>
        public string EncodingType { get; }

        /// <summary>
        /// Gets the value of the SecurityKeyIdentifier.
        /// </summary>
        public string Value { get; set; }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">The object to compare with the current object.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is not SecurityKeyIdentifier other)
                return false;

            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return string.Equals(ValueType, other.ValueType) &&
                   string.Equals(EncodingType, other.EncodingType) &&
                   string.Equals(Value, other.Value);
        }

        /// <summary>
        /// Serves as the default hash function.
        /// </summary>
        /// <returns>A hash code for the current object.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(ValueType, EncodingType, Value);
        }
    }
}
