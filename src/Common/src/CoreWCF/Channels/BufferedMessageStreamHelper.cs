// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    /// <summary>
    /// Reads a message body into a single pooled buffer, for transports that buffer a stream whose length is
    /// not known in advance (chunked HTTP being the case that reaches this today).
    /// </summary>
    /// <remarks>
    /// Lives in <c>src/Common</c> because the logic is needed from more than one assembly and the repo
    /// deliberately ships no <c>InternalsVisibleTo</c>. Like <see cref="BufferManagerBufferWriter"/> it is also
    /// compiled straight into CoreWCF.Primitives.Tests, so it must not reference <c>SR</c>/<c>SRCommon</c> or
    /// the per-assembly diagnostics helpers &#8212; hence the injected exception factory.
    /// </remarks>
    internal static class BufferedMessageStreamHelper
    {
        /// <summary>
        /// Reads <paramref name="stream"/> to the end, or until <paramref name="maxBufferSize"/> is exceeded.
        /// </summary>
        /// <param name="stream">The body stream. It is disposed on end-of-stream, matching the behaviour of the
        /// hand-written loops this replaces; it is left alone on the quota path.</param>
        /// <param name="bufferManager">The transport's pool. The returned array belongs to it, and goes back to
        /// it when the <c>Message</c> built from the segment is closed.</param>
        /// <param name="maxBufferSize">Ceiling on the buffered body.</param>
        /// <param name="initialBufferSize">Size requested from the pool up front.</param>
        /// <param name="createQuotaExceededException">Builds the exception thrown when
        /// <paramref name="maxBufferSize"/> is exceeded.</param>
        /// <returns>The raw pooled array with the byte count read. Ownership passes to the caller, which hands
        /// it to <c>MessageEncoder.ReadMessage</c> together with the same <paramref name="bufferManager"/>.</returns>
        internal static async Task<ArraySegment<byte>> BufferMessageStreamAsync(Stream stream,
            BufferManager bufferManager, int maxBufferSize, int initialBufferSize,
            Func<int, Exception> createQuotaExceededException)
        {
            // The using block is the one behaviour change: the loops this replaces had no try, so a throw out
            // of ReadAsync - or out of the quota check itself - leaked the rented buffer.
            using (BufferManagerBufferWriter writer = new BufferManagerBufferWriter(bufferManager,
                initialBufferSize, maxBufferSize, maxBufferSize, createQuotaExceededException))
            {
                // A maxBufferSize of 0 reads nothing and does not throw, as before.
                while (writer.FreeCapacity > 0)
                {
                    Memory<byte> free = writer.GetMemory(1);
                    if (!MemoryMarshal.TryGetArray<byte>(free, out ArraySegment<byte> segment))
                    {
                        // Unreachable: the writer is always array backed.
                        throw new InvalidOperationException("The buffer writer did not expose an array segment.");
                    }

                    // netstandard2.0 Stream has no Memory<byte> overload, so read through the array.
                    int count = await stream.ReadAsync(segment.Array, segment.Offset, segment.Count);
                    if (count == 0)
                    {
                        stream.Dispose();
                        break;
                    }

                    writer.Advance(count);

                    // The ceiling is checked as soon as the buffer fills, before another read is attempted, so
                    // a body of exactly maxBufferSize bytes followed by end-of-stream still throws.
                    if (writer.FreeCapacity == 0 && !writer.TryGrow())
                    {
                        throw writer.CreateQuotaExceededException();
                    }
                }

                byte[] buffer = writer.DetachBuffer(out int writtenCount);
                return new ArraySegment<byte>(buffer, 0, writtenCount);
            }
        }
    }
}
