// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.Security;

namespace CoreWCF.Channels
{
    internal class ProtectionLevelHelper
    {
        internal static bool IsDefined(ProtectionLevel value)
        {
            return (value == ProtectionLevel.None
                    || value == ProtectionLevel.Sign
                    || value == ProtectionLevel.EncryptAndSign);
        }
    }
}
