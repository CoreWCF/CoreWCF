// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Channels;

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
