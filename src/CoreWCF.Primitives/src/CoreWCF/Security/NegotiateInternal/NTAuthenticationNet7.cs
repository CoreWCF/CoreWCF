// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.NegotiateInternal
{
    internal class NTAuthenticationNet7 : NTAuthenticationNet5
    {
        protected override int EncryptInternal(byte[] input, ref byte[] output)
        {
            /*
             * int Encrypt(
             *     ReadOnlySpan<byte> buffer,
             *     [NotNull] ref byte[]? output)
             */
            object[] parameters = new object[] { Instance, input, output };
            int totalBytes = (int)s_encryptInvoker.DynamicInvoke(parameters);
            output = (byte[])parameters[2];
            return totalBytes;
        }
    }
}
