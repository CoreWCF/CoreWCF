// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum CommunicationState
    {
        Faulted = 5,
        Closed = 4,
        Closing = 3,
        Opened = 2,
        Opening = 1,
        Created = 0,
    }
}