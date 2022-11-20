// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Security.NegotiateInternal
{
    internal class NTAuthenticationNet7 : NTAuthenticationNet5
    {
        /*
         * int Encrypt(
         *     ReadOnlySpan<byte> buffer,
         *     [NotNull] ref byte[]? output)
         */
        private static readonly Lazy<EncryptInvoker> s_encryptInvoker = new Lazy<EncryptInvoker>(() => BuildEncrypt());

        public override int Encrypt(byte[] input, ref byte[] output) => s_encryptInvoker.Value(Instance, input, ref output);
    }
}
