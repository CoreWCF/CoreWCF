// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    internal static class MsmqEncryptionAlgorithmHelper
    {
        public static bool IsDefined(MsmqEncryptionAlgorithm algorithm)
        {
            return algorithm == MsmqEncryptionAlgorithm.RC4Stream || algorithm == MsmqEncryptionAlgorithm.Aes;
        }
    }
}
