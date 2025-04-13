// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq.Expressions;
using System.Linq;
using System.Reflection;

namespace CoreWCF.Security.NegotiateInternal
{
    internal class NTAuthenticationNet5 : NTAuthenticationLegacy
    {
        protected delegate int EncryptInvoker(object instance, byte[] buffer, ref byte[] output);

        protected static readonly Delegate s_encryptInvoker = LambdaExpressionBuilder.BuildFor(s_ntAuthenticationType, s_encrypt).Compile();

        protected override int EncryptInternal(byte[] input, ref byte[] output)
        {
            /*
             * int Encrypt(
             *     ReadOnlySpan<byte> buffer, 
             *     [NotNull] ref byte[]? output, 
             *     uint sequenceNumber)
             */
            object[] parameters = new object[] { Instance, input, output, 0U };
            int totalBytes = (int)s_encryptInvoker.DynamicInvoke(parameters);
            output = (byte[])parameters[2];
            return totalBytes;
        }
    }
}
