// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    internal enum FramingRecordType
    {
        Version = 0x0,
        Mode = 0x1,
        Via = 0x2,
        KnownEncoding = 0x3,
        ExtensibleEncoding = 0x4,
        UnsizedEnvelope = 0x5,
        SizedEnvelope = 0x6,
        End = 0x7,
        Fault = 0x8,
        UpgradeRequest = 0x9,
        UpgradeResponse = 0xA,
        PreambleAck = 0xB,
        PreambleEnd = 0xC,
    }
}
