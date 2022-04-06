// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// When caching an <see cref="SessionSecurityToken"/> there are two indexes required. One is the ContextId
    /// that is unique across all <see cref="SessionSecurityToken"/> and the next is KeyGeneration which is 
    /// unique within a session. When an <see cref="SessionSecurityToken"/> is issued it has only a ContextId. When
    /// the <see cref="SessionSecurityToken"/> is renewed the KeyGeneration is added as an second index to the
    /// <see cref="SessionSecurityToken"/>. Now the renewed <see cref="SessionSecurityToken"/> is uniquely identifiable via the ContextId and 
    /// KeyGeneration. 
    /// The class <see cref="SessionSecurityTokenCacheKey"/> is used as the index
    /// to the <see cref="SessionSecurityToken"/> cache. This index will always have a valid ContextId specified 
    /// but the KeyGeneration may be null. There is also an optional EndpointId
    /// which gives the endpoint to which the token is scoped.
    /// </summary>
    public class SessionSecurityTokenCacheKey
    {
        /// <summary>
        /// Creates an instance of <see cref="SessionSecurityTokenCacheKey"/> which
        /// is used as an index while caching <see cref="SessionSecurityToken"/>.
        /// </summary>
        /// <param name="endpointId">The endpoint Id to which the <see cref="SessionSecurityToken"/> is scoped.</param>
        /// <param name="contextId">UniqueId of the <see cref="SessionSecurityToken"/>.</param>
        /// <param name="keyGeneration">UniqueId which is available when the <see cref="SessionSecurityToken"/> is renewed. Will be
        /// null when caching a new <see cref="SessionSecurityToken"/>.</param>
        public SessionSecurityTokenCacheKey(string endpointId, System.Xml.UniqueId contextId, System.Xml.UniqueId keyGeneration)
        {
            EndpointId = endpointId ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpointId));
            ContextId = contextId ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contextId));
            KeyGeneration = keyGeneration;
        }

        /// <summary>
        /// Gets or sets a value indicating whether KeyGeneration can be ignored
        /// while doing index comparison.
        /// </summary>
        public bool IgnoreKeyGeneration { get; set; }

        /// <summary>
        /// Gets the ContextId of the <see cref="SessionSecurityToken"/>
        /// </summary>
        public System.Xml.UniqueId ContextId { get; }

        /// <summary>
        /// Gets the EndpointId to which this cache entry is scoped.
        /// </summary>
        public string EndpointId { get; }

        /// <summary>
        /// Gets the KeyGeneration of the <see cref="SessionSecurityToken"/>
        /// </summary>
        public System.Xml.UniqueId KeyGeneration { get; }

        /// <summary>
        /// Implements the equality operator for <see cref="SessionSecurityTokenCacheKey"/>.
        /// </summary>
        /// <param name="first">First object to compare.</param>
        /// <param name="second">Second object to compare.</param>
        /// <returns>'true' if both objects are equal.</returns>
        public static bool operator ==(SessionSecurityTokenCacheKey first, SessionSecurityTokenCacheKey second)
        {
            if (ReferenceEquals(first, null))
            {
                return ReferenceEquals(second, null);
            }

            return first.Equals(second);
        }

        /// <summary>
        /// Implements the inequality operator for <see cref="SessionSecurityTokenCacheKey"/>.
        /// </summary>
        /// <param name="first">First object to compare.</param>
        /// <param name="second">Second object to compare.</param>
        /// <returns>'true' if both the objects are different.</returns>
        public static bool operator !=(SessionSecurityTokenCacheKey first, SessionSecurityTokenCacheKey second)
        {
            return !(first == second);
        }
        
        /// <summary>
        /// Checks if the given object is the same as the current object.
        /// </summary>
        /// <param name="obj">The object to be compared.</param>
        /// <returns>'true' if both are the same object else false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is SessionSecurityTokenCacheKey)
            {
                SessionSecurityTokenCacheKey key2 = obj as SessionSecurityTokenCacheKey;
                if (key2.ContextId != ContextId)
                {
                    return false;
                }

                if (!StringComparer.Ordinal.Equals(key2.EndpointId, EndpointId))
                {
                    return false;
                }
                
                // If KeyGeneration can be ignored on either one of them then we
                // don't do KeyGeneration comparison.
                if (!IgnoreKeyGeneration && !key2.IgnoreKeyGeneration)
                {
                    return key2.KeyGeneration == KeyGeneration;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns a Hash code for this object.
        /// </summary>
        /// <returns>Hash code for the object as a Integer.</returns>
        public override int GetHashCode()
        {
            if (KeyGeneration == null)
            {
                return ContextId.GetHashCode();
            }
            else
            {
                return ContextId.GetHashCode() ^ KeyGeneration.GetHashCode();
            }
        }

        /// <summary>
        /// Implements ToString() to provide a unique identifier.
        /// </summary>
        /// <returns>This key, in string form.</returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(EndpointId);
            sb.Append(';');
            sb.Append(ContextId.ToString());
            sb.Append(';');
            if (!IgnoreKeyGeneration && KeyGeneration != null)
            {
                sb.Append(KeyGeneration.ToString());
            }

            return sb.ToString();
        }       
    }
}
