using CoreWCF.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF
{
    internal class GeneralEndpointIdentity : EndpointIdentity
    {
        public GeneralEndpointIdentity(Claim identityClaim)
        {
            base.Initialize(identityClaim);
        }
    }
}
