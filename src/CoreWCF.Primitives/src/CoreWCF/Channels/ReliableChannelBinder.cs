// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    internal enum TolerateFaultsMode
    {
        Never,
        IfNotSecuritySession,
        Always
    }

    [Flags]
    internal enum MaskingMode
    {
        None = 0x0,
        Handled = 0x1,
        Unhandled = 0x2,
        All = Handled | Unhandled
    }
}
