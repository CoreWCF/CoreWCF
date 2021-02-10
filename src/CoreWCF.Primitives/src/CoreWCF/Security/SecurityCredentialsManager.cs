// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Selectors;

namespace CoreWCF.Security
{
    public abstract class SecurityCredentialsManager
    {
        protected SecurityCredentialsManager() { }

        // TODO: Resolve solution to SecurityTokenManager which lives in System.IdentityModel.Selectors not existing on .Net standard
        internal abstract SecurityTokenManager CreateSecurityTokenManager();
    }
}
