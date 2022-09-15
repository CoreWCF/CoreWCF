// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license

using System;
using System.IO;
using System.Net.Http;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class XmlStreamedByteStreamReader : XmlByteStreamReader
    {
        protected XmlStreamedByteStreamReader(XmlDictionaryReaderQuotas quotas)
            : base(quotas)
        {
        }

        public static XmlStreamedByteStreamReader Create(Stream stream, XmlDictionaryReaderQuotas quotas) => new StreamXmlStreamedByteStreamReader(stream, quotas);

        public static XmlStreamedByteStreamReader Create(HttpRequestMessage httpRequestMessage, XmlDictionaryReaderQuotas quotas) => new HttpRequestMessageStreamedBodyReader(httpRequestMessage, quotas);

        public static XmlStreamedByteStreamReader Create(HttpResponseMessage httpResponseMessage, XmlDictionaryReaderQuotas quotas) => new HttpResponseMessageStreamedBodyReader(httpResponseMessage, quotas);

        protected override void OnClose()
        {
            ReleaseStream();
            base.OnClose();
        }

        public override int ReadContentAsBase64(byte[] buffer, int index, int count)
        {
            EnsureInContent();
            ByteStreamMessageUtility.EnsureByteBoundaries(buffer, index, count, true);

            if (count == 0)
            {
                return 0;
            }

            Stream stream = GetStream();
            int numBytesRead = stream.Read(buffer, index, count);
            if (numBytesRead == 0)
            {
                position = ReaderPosition.EndElement;
            }

            return numBytesRead;
        }

        protected override byte[] OnToByteArray()
        {
            throw Fx.Exception.AsError(
                  new InvalidOperationException(SR.GetByteArrayFromStreamContentNotAllowed));
        }

        protected override Stream OnToStream()
        {
            Stream result = GetStream();

            Fx.Assert(result != null, "The inner stream is null. Please check if the reader is closed or the ToStream method was already called before.");

            ReleaseStream();
            return result;
        }

        protected abstract Stream GetStream();

        protected abstract void ReleaseStream();

        public override bool TryGetBase64ContentLength(out int length)
        {
            // in ByteStream encoder, we're not concerned about individual xml nodes
            // therefore we can just return the entire length of the stream
            Stream stream = GetStream();
            if (!IsClosed && stream.CanSeek)
            {
                long streamLength = stream.Length;
                if (streamLength <= int.MaxValue)
                {
                    length = (int)streamLength;
                    return true;
                }
            }

            length = -1;
            return false;
        }

        internal class StreamXmlStreamedByteStreamReader : XmlStreamedByteStreamReader
        {
            private Stream _stream;

            public StreamXmlStreamedByteStreamReader(Stream stream, XmlDictionaryReaderQuotas quotas)
                : base(quotas)
            {
                Fx.Assert(stream != null, "The 'stream' parameter should not be null.");

                _stream = stream;
            }

            protected override Stream GetStream() => _stream;

            protected override void ReleaseStream()
            {
                _stream = null;
            }
        }

        internal class HttpRequestMessageStreamedBodyReader : XmlStreamedByteStreamReader
        {
            private HttpRequestMessage _httpRequestMessage;

            public HttpRequestMessageStreamedBodyReader(HttpRequestMessage httpRequestMessage, XmlDictionaryReaderQuotas quotas)
                : base(quotas)
            {
                Fx.Assert(httpRequestMessage != null, "The 'httpRequestMessage' parameter should not be null.");

                _httpRequestMessage = httpRequestMessage;
            }

            protected override Stream GetStream()
            {
                if (_httpRequestMessage == null)
                {
                    return null;
                }

                HttpContent content = _httpRequestMessage.Content;
                if (content != null)
                {
                    return content.ReadAsStreamAsync().Result;
                }

                return new MemoryStream(Array.Empty<byte>());
            }

            protected override void ReleaseStream()
            {
                _httpRequestMessage = null;
            }
        }

        internal class HttpResponseMessageStreamedBodyReader : XmlStreamedByteStreamReader
        {
            private HttpResponseMessage _httpResponseMessage;

            public HttpResponseMessageStreamedBodyReader(HttpResponseMessage httpResponseMessage, XmlDictionaryReaderQuotas quotas)
                : base(quotas)
            {
                Fx.Assert(httpResponseMessage != null, "The 'httpResponseMessage' parameter should not be null.");

                _httpResponseMessage = httpResponseMessage;
            }

            protected override Stream GetStream()
            {
                if (_httpResponseMessage == null)
                {
                    return null;
                }

                HttpContent content = _httpResponseMessage.Content;
                if (content != null)
                {
                    return content.ReadAsStreamAsync().Result;
                }

                return new MemoryStream(Array.Empty<byte>());
            }

            protected override void ReleaseStream()
            {
                _httpResponseMessage = null;
            }
        }
    }
}
