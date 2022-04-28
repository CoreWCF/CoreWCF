// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;

namespace CoreWCF.Channels
{
    public abstract class StreamBodyWriter : BodyWriter
    {
        protected StreamBodyWriter(bool isBuffered)
            : base(isBuffered)
        { }

        protected abstract void OnWriteBodyContents(Stream stream);

        protected override BodyWriter OnCreateBufferedCopy(int maxBufferSize)
        {
            using (BufferManagerOutputStream bufferedStream = new BufferManagerOutputStream(SR.MaxReceivedMessageSizeExceeded, maxBufferSize))
            {
                OnWriteBodyContents(bufferedStream);
                byte[] bytesArray = bufferedStream.ToArray(out int size);
                return new BufferedBytesStreamBodyWriter(bytesArray, size);
            }
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            using (XmlWriterBackedStream stream = new XmlWriterBackedStream(writer))
            {
                OnWriteBodyContents(stream);
            }
        }

        internal class XmlWriterBackedStream : Stream
        {
            private const string StreamElementName = "Binary";
            private readonly XmlWriter _writer;

            public XmlWriterBackedStream(XmlWriter writer)
            {
                if (writer == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
                }

                _writer = writer;
            }

            public override bool CanRead
            {
                get { return false; }
            }

            public override bool CanSeek
            {
                get { return false; }
            }

            public override bool CanWrite
            {
                get { return true; }
            }

            public override void Flush()
            {
                _writer.Flush();
            }

            public override long Length
            {
                get
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.XmlWriterBackedStreamPropertyGetNotSupported, "Length")));
                }
            }

            public override long Position
            {
                get
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.XmlWriterBackedStreamPropertyGetNotSupported, "Position")));
                }
                set
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.XmlWriterBackedStreamPropertySetNotSupported, "Position")));
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.XmlWriterBackedStreamMethodNotSupported, "Read")));
            }

            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.XmlWriterBackedStreamMethodNotSupported, "BeginRead")));
            }

            public override int EndRead(IAsyncResult asyncResult)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.XmlWriterBackedStreamMethodNotSupported, "EndRead")));
            }

            public override int ReadByte()
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.XmlWriterBackedStreamMethodNotSupported, "ReadByte")));
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.XmlWriterBackedStreamMethodNotSupported, "Seek")));
            }

            public override void SetLength(long value)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.XmlWriterBackedStreamMethodNotSupported, "SetLength")));
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (_writer.WriteState == WriteState.Content)
                {
                    _writer.WriteBase64(buffer, offset, count);
                }
                else if (_writer.WriteState == WriteState.Start)
                {
                    _writer.WriteStartElement(StreamElementName, string.Empty);
                    _writer.WriteBase64(buffer, offset, count);
                }
            }
        }

        internal class BufferedBytesStreamBodyWriter : StreamBodyWriter
        {
            private readonly byte[] _array;
            private readonly int _size;

            public BufferedBytesStreamBodyWriter(byte[] array, int size)
                : base(true)
            {
                _array = array;
                _size = size;
            }

            protected override void OnWriteBodyContents(Stream stream)
            {
                stream.Write(_array, 0, _size);
            }
        }
    }
}
