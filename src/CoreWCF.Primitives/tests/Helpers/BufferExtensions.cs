// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Xunit;

namespace Helpers
{
    internal static class BufferExtensions
    {
        public static void FillWithData(this ArraySegment<byte> buffer)
        {
            for (int i = 0; i < buffer.Count; i++)
            {
                buffer.Array[buffer.Offset + i] = (byte)(i % byte.MaxValue);
            }
        }
    }
}
