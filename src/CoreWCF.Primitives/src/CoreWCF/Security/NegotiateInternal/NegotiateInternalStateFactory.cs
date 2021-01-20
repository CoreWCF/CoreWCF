using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Security.NegotiateInternal
{
    class NegotiateInternalStateFactory : INegotiateInternalStateFactory
    {
        public INegotiateInternalState CreateInstance()
        {
            return new NegotiateInternalState();
        }
    }
}
