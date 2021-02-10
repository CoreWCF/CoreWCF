// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    // This interface needs to be implemented by requests that need to save 
    // the RequestReplyCorrelatorKey into the request during RequestReplyCorrelator.Add
    // operation. 
    internal interface ICorrelatorKey
    {
        RequestReplyCorrelator.Key RequestCorrelatorKey { get; set; }
    }
}