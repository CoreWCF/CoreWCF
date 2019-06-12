using CoreWCF.Channels;
using CoreWCF.IdentityModel.Policy;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CoreWCF
{
    public class ServiceAuthenticationManager
    {
        public virtual ReadOnlyCollection<IAuthorizationPolicy> Authenticate(ReadOnlyCollection<IAuthorizationPolicy> authPolicy, Uri listenUri, ref Message message)
        {
            return authPolicy;
        }
    }
}
