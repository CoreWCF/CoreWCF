// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF
{
    public enum SecurityMode
    {
        None = 0,
        Transport = 1,
        Message = 2,
        TransportWithMessageCredential = 3,
    }

    [Flags]
    internal enum UnifiedSecurityMode
    {
        None = 0x001,
        Transport = 0x004,
        Message = 0x008,
        Both = 0x010,
        TransportWithMessageCredential = 0x020,
        TransportCredentialOnly = 0x040,
    }
}
