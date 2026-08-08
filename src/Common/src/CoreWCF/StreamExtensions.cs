// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace CoreWCF;

internal static class StreamExtensions
{
    public static void Write(this Stream stream, ReadOnlyMemory<byte> memory)
    {
        if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
        {
            stream.Write(segment.Array, segment.Offset, segment.Count);
        }
        else
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(memory.Length);
            memory.CopyTo(buffer);
            try
            {
                stream.Write(buffer, 0, memory.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}


