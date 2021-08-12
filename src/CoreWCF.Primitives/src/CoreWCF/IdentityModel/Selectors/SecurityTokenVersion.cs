// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;

namespace CoreWCF.IdentityModel.Selectors
{
    public abstract class SecurityTokenVersion
    {
        public abstract ReadOnlyCollection<string> GetSecuritySpecifications();
    }
}
