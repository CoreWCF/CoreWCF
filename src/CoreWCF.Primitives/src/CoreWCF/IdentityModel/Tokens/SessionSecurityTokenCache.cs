// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF.IdentityModel.Configuration;
using System.Xml;
using System;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary> 
    /// Defines a simple interface to a cache of security tokens.
    /// </summary>
    public abstract class SessionSecurityTokenCache //: ICustomIdentityConfiguration
    {
        /// <summary>
        /// Attempts to add an entry to the cache or update an existing one.
        /// </summary>
        /// <param name="key">The key of the entry to be added.</param>
        /// <param name="value">The associated SessionSecurityToken to be added.</param>
        /// <param name="expiryTime">The expiration time of the entry.</param>
        public abstract void AddOrUpdate(SessionSecurityTokenCacheKey key, SessionSecurityToken value, DateTime expiryTime);
       
        /// <summary>
        /// Retrieves all tokens associated with a given key.
        /// </summary>
        /// <param name="endpointId">The endpointId to search for.</param>
        /// <param name="contextId">The contextId to search for.</param>
        /// <returns>In the derived class returns, the collection of tokens associated with the key.</returns>
        public abstract IEnumerable<SessionSecurityToken> GetAll(string endpointId, System.Xml.UniqueId contextId);
       
        /// <summary>
        /// Attempts to retrieve an entry from the cache.
        /// </summary>
        /// <param name="key">The key of the entry to be retrieved.</param>
        /// <returns>The SessionSecurityToken associated with the input key, null if not match is found.</returns>
        public abstract SessionSecurityToken Get(SessionSecurityTokenCacheKey key);
       
        /// <summary>
        /// Attempts to remove all matching entries from cache.
        /// </summary>
        /// <param name="endpointId">The endpoint id for the entry to be removed.</param>
        /// <param name="contextId">The context Id for the entry to be removed.</param>
        public abstract void RemoveAll(string endpointId, System.Xml.UniqueId contextId);
        
        /// <summary>
        /// Attempts to remove all entries with a matching endpoint Id from the cache.
        /// </summary>
        /// <param name="endpointId">The endpoint id for the entry to be removed.</param>
        public abstract void RemoveAll(string endpointId);
               
        /// <summary>
        /// Attempts to remove an entry from the cache.
        /// </summary>
        /// <param name="key">The key of the entry to be removed.</param>
        public abstract void Remove(SessionSecurityTokenCacheKey key);
    }
}
