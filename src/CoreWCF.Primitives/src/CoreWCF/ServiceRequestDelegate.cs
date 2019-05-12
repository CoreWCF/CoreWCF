using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CoreWCF
{
    //
    // Summary:
    //     A function that can process a ServiceModel request.
    //
    // Parameters:
    //   context:
    //     The CoreWCF.Channels.RequestContext for the request.
    //
    // Returns:
    //     A task that represents the completion of request processing.
    public delegate Task ServiceRequestDelegate(RequestContext context);
}
