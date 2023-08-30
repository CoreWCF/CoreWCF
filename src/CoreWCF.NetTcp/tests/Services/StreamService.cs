// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using Contract;
using Helpers;

namespace Services
{
    internal class StreamService : IStreamService
    {
        public Task<Stream> GetStreamAsync(long streamSize) => Task.FromResult<Stream>(new FixedLengthDataGeneratingStream(streamSize));
        public async Task<long> SendStreamAsync(Stream requestStream)
        {
            var arrayBuffer = ArrayPool<byte>.Shared.Rent(16384);
            try
            {
                long totalBytesRead = 0;
                while (true)
                {
                    int bytesRead = await requestStream.ReadAsync(arrayBuffer, 0, 16384);
                    if (bytesRead == 0) return totalBytesRead;
                    totalBytesRead += bytesRead;
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(arrayBuffer);
            }
        }
    }
}
