// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    internal enum FramingMode
    {
        Singleton = 0x1,
        Duplex = 0x2,
        Simplex = 0x3,
        SingletonSized = 0x4,
    }
}
