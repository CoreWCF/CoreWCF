// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Selectors;

namespace CoreWCF.Security
{
    internal interface IEndpointIdentityProvider
    {
        EndpointIdentity GetIdentityOfSelf(SecurityTokenRequirement tokenRequirement);
    }
}
