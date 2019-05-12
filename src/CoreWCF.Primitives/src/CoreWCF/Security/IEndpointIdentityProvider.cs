using CoreWCF.IdentityModel.Selectors;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Security
{
    internal interface IEndpointIdentityProvider
    {
        EndpointIdentity GetIdentityOfSelf(SecurityTokenRequirement tokenRequirement);
    }
}
