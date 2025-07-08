// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal static class MessageEncoderCompressionHandler
    {
        internal const string GZipContentEncoding = "gzip";
        internal const string DeflateContentEncoding = "deflate";

        internal static ReadOnlySequence<byte> DecompressBuffer(ReadOnlySequence<byte> buffer, BufferManager bufferManager, CompressionFormat compressionFormat, long maxReceivedMessageSize)
        {
            if (buffer.Length > maxReceivedMessageSize)
            {
                throw Fx.Exception.AsError(new QuotaExceededException(SR.Format(SRCommon.MaxReceivedMessageSizeExceeded, maxReceivedMessageSize)));
            }

            using Stream inputStream = PipeReader.Create(buffer).AsStream();
            using Stream gZipStream =
                compressionFormat == CompressionFormat.GZip
                    ? new GZipStream(inputStream, CompressionMode.Decompress)
                    : new DeflateStream(inputStream, CompressionMode.Decompress);

            int maxDecompressedSize = (int)Math.Min(maxReceivedMessageSize, int.MaxValue);

            using BufferManagerOutputStream bufferedOutStream = new (SRCommon.MaxReceivedMessageSizeExceeded, 1024, maxDecompressedSize,
                    bufferManager);
            gZipStream.CopyTo(bufferedOutStream);
            byte[] decompressedBytes = bufferedOutStream.ToArray(out int length);
            return new ReadOnlySequence<byte>(decompressedBytes, 0, length);
        }

        internal static void CompressBuffer(ref ArraySegment<byte> buffer, BufferManager bufferManager, CompressionFormat compressionFormat)
        {
            using (BufferManagerOutputStream bufferedOutStream = new BufferManagerOutputStream(SRCommon.MaxSentMessageSizeExceeded, 1024, int.MaxValue, bufferManager))
            {
                bufferedOutStream.Write(buffer.Array, 0, buffer.Offset);

                using (Stream ds = compressionFormat == CompressionFormat.GZip ?
                    (Stream)new GZipStream(bufferedOutStream, CompressionMode.Compress, true) :
                    (Stream)new DeflateStream(bufferedOutStream, CompressionMode.Compress, true))
                {
                    ds.Write(buffer.Array, buffer.Offset, buffer.Count);
                }

                byte[] compressedBytes = bufferedOutStream.ToArray(out int length);
                bufferManager.ReturnBuffer(buffer.Array);
                buffer = new ArraySegment<byte>(compressedBytes, buffer.Offset, length - buffer.Offset);
            }
        }

        internal static Stream GetDecompressStream(Stream compressedStream, CompressionFormat compressionFormat)
        {
            return compressionFormat == CompressionFormat.GZip ?
                    (Stream)new GZipStream(compressedStream, CompressionMode.Decompress, false) :
                    (Stream)new DeflateStream(compressedStream, CompressionMode.Decompress, false);
        }

        internal static Stream GetCompressStream(Stream uncompressedStream, CompressionFormat compressionFormat)
        {
            return compressionFormat == CompressionFormat.GZip ?
                    (Stream)new GZipStream(uncompressedStream, CompressionMode.Compress, true) :
                    (Stream)new DeflateStream(uncompressedStream, CompressionMode.Compress, true);
        }
    }
}
