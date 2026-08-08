// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class BinaryEncoderSessionTests
    {
        // A session message starts with the dictionary size, then that many bytes of dictionary,
        // each entry being its UTF-8 length followed by its bytes:
        //
        //   05            dictionary is 5 bytes long
        //   05 61 61 61 61   an entry declaring 5 bytes of UTF-8, with only 4 left to read
        //
        // The declared size fits the dictionary as a whole but not what remains once its own length
        // prefix has been consumed, which is the window where a size check placed before the prefix
        // is consumed lets malformed data through and faults on the slice instead of reporting it.
        [Fact]
        public async Task DictionaryEntryLongerThanWhatFollowsIt_ReportsMalformedSession()
        {
            byte[] malformedSession = { 0x05, 0x05, 0x61, 0x61, 0x61, 0x61 };

            MessageEncoder encoder = new BinaryMessageEncodingBindingElement()
                .CreateMessageEncoderFactory()
                .CreateSessionEncoder();

            await Assert.ThrowsAsync<InvalidDataException>(
                () => encoder.ReadMessageAsync(
                    new ReadOnlySequence<byte>(malformedSession),
                    BufferManager.CreateBufferManager(int.MaxValue, int.MaxValue),
                    encoder.ContentType).AsTask());
        }
    }
}
