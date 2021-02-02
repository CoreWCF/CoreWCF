// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Claims;

namespace CoreWCF
{
    internal class GeneralEndpointIdentity : EndpointIdentity
    {
        public GeneralEndpointIdentity(Claim identityClaim)
        {
            Initialize(identityClaim);
        }
    }
}
