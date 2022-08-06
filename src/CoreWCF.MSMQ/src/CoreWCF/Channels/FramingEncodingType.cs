// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace CoreWCF.Channels
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal enum FramingEncodingType
    {
        Soap11Utf8 = 0x0,
        Soap11Utf16 = 0x1,
        Soap11Utf16FFFE = 0x2,
        Soap12Utf8 = 0x3,
        Soap12Utf16 = 0x4,
        Soap12Utf16FFFE = 0x5,
        MTOM = 0x6,
        Binary = 0x7,
        BinarySession = 0x8,
    }
}
