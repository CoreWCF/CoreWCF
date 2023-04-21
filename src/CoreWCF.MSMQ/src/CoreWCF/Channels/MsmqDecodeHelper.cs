// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipelines;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoreWCF.Channels
{
    public static class MsmqDecodeHelper
    {
        private const int DefaultMaxViaSize = 2048;
        private const int DefaultMaxContentTypeSize = 256;

        public static async Task DecodeTransportDatagram(PipeReader pipeReader)
        {
            var serverModeDecoder = new ServerModeDecoder(NullLogger.Instance);
            await serverModeDecoder.ReadModeAsync(pipeReader);
            var decoder =
                new ServerSingletonSizedDecoder(DefaultMaxViaSize, DefaultMaxContentTypeSize, NullLogger.Instance);
            var readResult = await pipeReader.ReadAsync();
            if (readResult.IsCompleted)
            {
                return;
            }

            var buffer = readResult.Buffer;

            try
            {
                do
                {
                    if (buffer.Length <= 0)
                    {
                        throw decoder.CreatePrematureEOFException();
                    }

                    int decoded = decoder.Decode(buffer);
                    buffer = buffer.Slice(decoded);
                } while (decoder.CurrentState != ServerSingletonSizedDecoder.State.Start);

                pipeReader.AdvanceTo(buffer.Start);
            }
            catch (ProtocolException ex)
            {
                throw new MsmqPoisonMessageException(0, ex);
            }
        }
    }
}
