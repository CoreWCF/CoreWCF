// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Xml;

namespace CoreWCF.Security.Tokens
{
    public interface ISecurityContextSecurityTokenCache
    {
        void AddContext(SecurityContextSecurityToken token);
        bool TryAddContext(SecurityContextSecurityToken token);
        void ClearContexts();
        void RemoveContext(UniqueId contextId, UniqueId generation);
        void RemoveAllContexts(UniqueId contextId);
        SecurityContextSecurityToken GetContext(UniqueId contextId, UniqueId generation);
        Collection<SecurityContextSecurityToken> GetAllContexts(UniqueId contextId);
        void UpdateContextCachingTime(SecurityContextSecurityToken context, DateTime expirationTime);
    }
}
