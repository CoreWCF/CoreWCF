// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public enum MessageState
    {
        Closed = 4,
        Copied = 3,
        Created = 0,
        Read = 1,
        Written = 2,
    }
}