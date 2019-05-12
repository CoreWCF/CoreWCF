﻿using CoreWCF.IdentityModel.Selectors;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Security
{
    public abstract class SecurityCredentialsManager
    {
        protected SecurityCredentialsManager() { }

        // TODO: Resolve solution to SecurityTokenManager which lives in System.IdentityModel.Selectors not existing on .Net standard
        internal abstract SecurityTokenManager CreateSecurityTokenManager();
    }
}
