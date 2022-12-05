// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    internal class MsmqSecureHashAlgorithmHelper
    {
        internal static bool IsDefined(MsmqSecureHashAlgorithm algorithm)
        {
            return algorithm == MsmqSecureHashAlgorithm.MD5 ||
                   algorithm == MsmqSecureHashAlgorithm.Sha1 ||
                   algorithm == MsmqSecureHashAlgorithm.Sha256 ||
                   algorithm == MsmqSecureHashAlgorithm.Sha512;
        }
    }
}
