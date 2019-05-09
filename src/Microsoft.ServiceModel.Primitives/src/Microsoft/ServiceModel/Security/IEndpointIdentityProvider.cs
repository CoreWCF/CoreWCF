using Microsoft.IdentityModel.Selectors;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Security
{
    internal interface IEndpointIdentityProvider
    {
        EndpointIdentity GetIdentityOfSelf(SecurityTokenRequirement tokenRequirement);
    }
}
