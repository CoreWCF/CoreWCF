using Microsoft.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel
{
    internal class GeneralEndpointIdentity : EndpointIdentity
    {
        public GeneralEndpointIdentity(Claim identityClaim)
        {
            base.Initialize(identityClaim);
        }
    }
}
