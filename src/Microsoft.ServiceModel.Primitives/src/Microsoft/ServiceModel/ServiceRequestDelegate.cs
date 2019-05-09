using Microsoft.ServiceModel.Channels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel
{
    //
    // Summary:
    //     A function that can process a ServiceModel request.
    //
    // Parameters:
    //   context:
    //     The System.ServiceModel.Channels.RequestContext for the request.
    //
    // Returns:
    //     A task that represents the completion of request processing.
    public delegate Task ServiceRequestDelegate(RequestContext context);
}
