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

        /*
         * int Encrypt(
         *     ReadOnlySpan<byte> buffer, 
         *     [NotNull] ref byte[]? output, 
         *     uint sequenceNumber)
         */
        private static readonly EncryptInvoker s_encryptInvoker = BuildEncrypt(Expression.Constant(0U));

        protected static EncryptInvoker BuildEncrypt(params Expression[] otherParameters)
        {
            /* This method build a function equivalent to:
             * 
             * (object instance, byte[] buffer, ref byte[] output) => 
             *     ((NTAuthentication) instance).Encrypt((ReadOnlySpan<byte>) buffer.AsSpan(), output, ...*otherParameters*)
             *                                                                                          
             */
            ParameterExpression instanceParameter = Expression.Parameter(typeof(object));
            ParameterExpression bufferParameter = Expression.Parameter(typeof(byte[]));
            ParameterExpression outputParameter = Expression.Parameter(typeof(byte[]).MakeByRefType());

            UnaryExpression typedInstance = Expression.TypeAs(instanceParameter, s_ntAuthenticationType);

            MethodInfo asSpan = typeof(MemoryExtensions)
                .GetMethods()
                .Single(m => m.Name == "AsSpan" && m.ToString() == "System.Span`1[T] AsSpan[T](T[])").
                MakeGenericMethod(typeof(byte));


            UnaryExpression bufferSpan = Expression.Convert(
                Expression.Call(asSpan, bufferParameter),
                typeof(ReadOnlySpan<byte>));

            MethodCallExpression call = Expression.Call(
                typedInstance,
                s_encrypt,
                Enumerable.Concat(new Expression[] { bufferSpan, outputParameter }, otherParameters));

            var expression = Expression.Lambda<EncryptInvoker>(call, instanceParameter, bufferParameter, outputParameter);

            return expression.Compile();
        }

        protected override int EncryptInternal(byte[] input, ref byte[] output) => s_encryptInvoker(Instance, input, ref output);
    }
}
