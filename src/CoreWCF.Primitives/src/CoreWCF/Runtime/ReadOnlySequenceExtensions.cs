// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace CoreWCF.Runtime;

public static class ReadOnlySequenceExtensions
{
    public static unsafe string ParseAsUTF8String(this ReadOnlySequence<byte> slice, int stringLength)
    {
        const int MaxStackAllocSize = 128;
        var decoder = Encoding.UTF8.GetDecoder();
        var preProcessedBytes = 0;
        var processedCharacters = 0;

        char[] rented = null;
        Span<char> characterSpan = (stringLength > MaxStackAllocSize
            ? rented = ArrayPool<char>.Shared.Rent(stringLength)
            : stackalloc char[MaxStackAllocSize]).Slice(0, stringLength);
        try
        {
            foreach (var memory in slice)
            {
                preProcessedBytes += memory.Length;
                bool isLast = preProcessedBytes == slice.Length;
                var emptyCharSlice =
                    characterSpan.Slice(processedCharacters);

                int charCount;
                fixed (byte* bytes = memory.Span)
                {
                    fixed (char* chars = emptyCharSlice)
                    {
                        charCount = decoder.GetChars(bytes, memory.Span.Length, chars, emptyCharSlice.Length, isLast);
                    }
                }

                processedCharacters += charCount;
            }

            var finalCharacters = characterSpan.Slice(0, processedCharacters);
            fixed (char* finalChars = finalCharacters)
            {
                return new string(finalChars, 0, processedCharacters);
            }
        }
        finally
        {
            if (rented != null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
