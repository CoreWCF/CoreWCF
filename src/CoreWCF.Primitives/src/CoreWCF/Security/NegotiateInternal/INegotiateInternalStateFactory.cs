using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Security.NegotiateInternal
{
    interface INegotiateInternalStateFactory
    {
        INegotiateInternalState CreateInstance();
    }
}
