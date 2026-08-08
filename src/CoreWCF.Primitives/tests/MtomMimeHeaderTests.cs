// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    /// <summary>
    /// Regression tests for https://github.com/CoreWCF/CoreWCF/issues/1764.
    ///
    /// MimeHeaderReader accumulates a MIME-part header name or value one chunk at a
    /// time: once per 1024-byte read buffer, and once more per folded continuation
    /// line. It used to concatenate those chunks onto an immutable string, so an
    /// N-char header cost O(N^2) copying. Because MimeHeaders.Add refunds the
    /// MaxBufferSize quota for every unrecognised header name, the per-header quota
    /// resets and the header count is unbounded, which let a small request expand into
    /// an arbitrary amount of copying and garbage.
    ///
    /// MimeHeaderReader is internal and reachable only through the MTOM decode path,
    /// so these tests drive it through MtomMessageEncoder rather than directly.
    /// </summary>
    public class MtomMimeHeaderTests
    {
        private const string CRLF = "\r\n";
        private const int BodyByteCount = 4096;
        private const int ReadBufferSize = 1024;

        // Emitted verbatim by MtomMessageEncoder for the root (xop+xml) part.
        private const string RootPartContentType = "Content-Type: application/xop+xml;charset=utf-8;type=\"text/xml\"";
        private const string RootPartContentId = "Content-ID: <http://tempuri.org/0>";

        /// <summary>
        /// Exercises AppendValue across both split mechanisms at once: the value is
        /// folded over several continuation lines and is long enough to span multiple
        /// 1024-byte read buffers. Content-Type is used deliberately because it is one
        /// of the few headers MimeHeaders keeps - a mis-assembled value fails to parse
        /// instead of being silently discarded.
        /// </summary>
        [Fact]
        public async Task FoldedAndBufferSpanningContentType_RoundTrips()
        {
            string payload = await BuildRootPartPayloadAsync(p => p.Replace(
                RootPartContentType,
                "Content-Type: application/xop+xml;" + CRLF +
                " charset=utf-8;" + CRLF +
                " x-ignored=\"" + new string('a', 2 * ReadBufferSize) + "\";" + CRLF +
                " type=\"text/xml\""));

            await AssertRoundTripsAsync(payload);
        }

        /// <summary>
        /// A header with no value at all is still a header that was read, so Read() must
        /// return true and header parsing must continue. Guards against treating
        /// "nothing accumulated" as "no header", which would silently drop every header
        /// after this one - including the Content-Type below it.
        /// </summary>
        [Fact]
        public async Task EmptyHeaderValue_DoesNotStopHeaderParsing()
        {
            string payload = await BuildRootPartPayloadAsync(p => InsertHeaders(p, "x-empty:" + CRLF));

            await AssertRoundTripsAsync(payload);
        }

        /// <summary>
        /// Exercises AppendName across a read-buffer boundary. An unrecognised name is
        /// discarded by MimeHeaders.Add, so the only observable assertion is that the
        /// message still parses; the value-side tests cover assembly accuracy.
        /// </summary>
        [Fact]
        public async Task HeaderNameSpanningReadBuffer_RoundTrips()
        {
            string payload = await BuildRootPartPayloadAsync(
                p => InsertHeaders(p, "x-" + new string('a', 2 * ReadBufferSize) + ": v" + CRLF));

            await AssertRoundTripsAsync(payload);
        }

        /// <summary>
        /// Exercises AppendValue across a read-buffer boundary with no folding.
        /// </summary>
        [Fact]
        public async Task HeaderValueSpanningReadBuffer_RoundTrips()
        {
            string payload = await BuildRootPartPayloadAsync(
                p => InsertHeaders(p, "x-long: " + new string('a', 4 * ReadBufferSize) + CRLF));

            await AssertRoundTripsAsync(payload);
        }

#if NET8_0_OR_GREATER
        /// <summary>
        /// The quadratic-behaviour guard. A single header folded four bytes at a time
        /// ("\r\n a") produces one AppendValue per fold, so the concatenating
        /// implementation allocated the sum of every intermediate string: roughly 500 MB
        /// from a 70 KB request at the ~32K char quota limit. Accumulating into a
        /// StringBuilder keeps it under 1 MB.
        ///
        /// Allocated bytes are asserted rather than wall-clock time because the counter
        /// is deterministic and insensitive to CI machine load. The work runs on a
        /// dedicated thread so the thread-local counter cannot be disturbed by an await
        /// resuming elsewhere or by other tests running in parallel.
        /// </summary>
        [Fact]
        public async Task HeavilyFoldedHeaderValue_DoesNotAllocateQuadratically()
        {
            // Each fold contributes 2 chars to the value, and
            // MtomEncoderDefaults.MaxBufferSize (65536) caps one header at ~32768 chars,
            // so stay just under that.
            const int foldCount = 15000;
            const long allocationCeiling = 20L * 1024 * 1024;

            var folded = new StringBuilder("x-pad: a");
            for (int i = 0; i < foldCount; i++)
            {
                folded.Append(CRLF).Append(" a");
            }

            folded.Append(CRLF);

            string payload = await BuildRootPartPayloadAsync(p => InsertHeaders(p, folded.ToString()));
            byte[] bytes = Encoding.UTF8.GetBytes(payload);

            long allocated = 0;
            Exception failure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    // Warm up first so JIT and first-use allocations are not attributed
                    // to the measured read.
                    ReadAndVerify(bytes);

                    long before = GC.GetAllocatedBytesForCurrentThread();
                    ReadAndVerify(bytes);
                    allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            worker.Start();
            worker.Join();

            Assert.Null(failure);
            Assert.True(
                allocated < allocationCeiling,
                $"Reading a {bytes.Length} byte MTOM message with a {(foldCount * 2) + 1} char folded header " +
                $"allocated {allocated / (1024.0 * 1024.0):F1} MB, exceeding the {allocationCeiling / (1024 * 1024)} MB " +
                "ceiling. MIME header accumulation is quadratic again.");
        }

        private static void ReadAndVerify(byte[] payload)
        {
            MessageEncoder encoder = CreateEncoder();
            Message message = encoder
                .ReadMessageAsync(new MemoryStream(payload), int.MaxValue, encoder.ContentType)
                .GetAwaiter().GetResult();

            VerifyBody(message);
        }
#endif

        private static MessageEncoder CreateEncoder() =>
            new MtomMessageEncodingBindingElement(MessageVersion.Soap11, Encoding.UTF8)
                .CreateMessageEncoderFactory().Encoder;

        /// <summary>
        /// Produces a genuine multipart/related MTOM payload with the encoder's own
        /// writer, then lets the caller rewrite the root part's header block. Writing a
        /// real message avoids hand-maintaining the boundary, start parameter and
        /// content ids.
        /// </summary>
        private static async Task<string> BuildRootPartPayloadAsync(Func<string, string> rewriteHeaders)
        {
            MessageEncoder encoder = CreateEncoder();
            Message message = Message.CreateMessage(
                MessageVersion.Soap11, "http://tempuri.org/IEcho/Echo", new FixedSizeBodyWriter(BodyByteCount));

            var stream = new MemoryStream();
            await encoder.WriteMessageAsync(message, stream);
            string payload = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Contains(RootPartContentType, payload);

            return rewriteHeaders(payload);
        }

        /// <summary>Inserts a header block at the top of the root part's headers.</summary>
        private static string InsertHeaders(string payload, string headerBlock)
        {
            int index = payload.IndexOf(RootPartContentId, StringComparison.Ordinal);
            Assert.True(index >= 0, "Root MIME part not found in the generated payload.");

            return payload.Insert(index, headerBlock);
        }

        private static async Task AssertRoundTripsAsync(string payload)
        {
            MessageEncoder encoder = CreateEncoder();
            var stream = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            Message message = await encoder.ReadMessageAsync(stream, int.MaxValue, encoder.ContentType);

            Assert.Equal("http://tempuri.org/IEcho/Echo", message.Headers.Action);
            VerifyBody(message);
        }

        private static void VerifyBody(Message message)
        {
            XmlDictionaryReader reader = message.GetReaderAtBodyContents();
            reader.ReadStartElement("Echo", "http://tempuri.org/");
            reader.ReadStartElement("data", "http://tempuri.org/");
            byte[] data = reader.ReadContentAsBase64();

            Assert.Equal(BodyByteCount, data.Length);
        }

        private class FixedSizeBodyWriter : BodyWriter
        {
            private readonly int _byteCount;

            public FixedSizeBodyWriter(int byteCount)
                : base(true)
            {
                _byteCount = byteCount;
            }

            protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
            {
                writer.WriteStartElement("Echo", "http://tempuri.org/");
                writer.WriteStartElement("data", "http://tempuri.org/");
                writer.WriteBase64(new byte[_byteCount], 0, _byteCount);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
        }
    }
}
