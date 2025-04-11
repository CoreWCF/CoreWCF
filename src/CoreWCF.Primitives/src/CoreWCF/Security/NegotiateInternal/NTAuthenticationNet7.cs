// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.NegotiateInternal
{
    internal class NTAuthenticationNet7 : NTAuthenticationNet5
    {
        /*
         * int Encrypt(
         *     ReadOnlySpan<byte> buffer,
         *     [NotNull] ref byte[]? output)
         */
        private static readonly EncryptInvoker s_encryptInvoker = BuildEncrypt();

        protected override int EncryptInternal(byte[] input, ref byte[] output) => s_encryptInvoker(Instance, input, ref output);
    }
}
