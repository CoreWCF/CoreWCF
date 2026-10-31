// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Reflection;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    // IntDecoder is an internal struct. Following the same approach as the other tests in this repo
    // that need internals, we reach it with minimal reflection rather than adding InternalsVisibleTo.
    public class IntDecoderTests
    {
        [Fact]
        public void MultiByteValueSpanningTheWholeBuffer_IsFullyDecoded()
        {
            // 0x80 0x80 0x01 is 16384 encoded over three bytes, in a buffer holding nothing else.
            // Decode slices the buffer as it advances, so taking the shrinking sequence as its own
            // budget stops it one byte short and leaves the value undecoded.
            byte[] encoded = { 0x80, 0x80, 0x01 };

            Type intDecoderType = typeof(MessageEncoder).Assembly
                .GetType("CoreWCF.Channels.IntDecoder", throwOnError: true);
            object decoder = Activator.CreateInstance(intDecoderType);
            MethodInfo decode = intDecoderType.GetMethod("Decode", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo value = intDecoderType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);

            int bytesConsumed = (int)decode.Invoke(decoder, new object[] { new ReadOnlySequence<byte>(encoded) });

            Assert.Equal(encoded.Length, bytesConsumed);
            Assert.Equal(16384, value.GetValue(decoder));
        }
    }
}
