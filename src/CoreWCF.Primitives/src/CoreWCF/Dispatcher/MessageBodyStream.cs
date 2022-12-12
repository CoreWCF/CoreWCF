// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    public class MessageBodyStream : Stream
    {
        private readonly Message _message;
        private XmlDictionaryReader _reader;
        private long _position;
        private readonly string _wrapperName, _wrapperNs;
        private readonly string _elementName, _elementNs;
        private readonly bool _isRequest;
        public MessageBodyStream(Message message, string wrapperName, string wrapperNs, string elementName, string elementNs, bool isRequest)
        {
            _message = message;
            _position = 0;
            _wrapperName = wrapperName;
            _wrapperNs = wrapperNs;
            _elementName = elementName;
            _elementNs = elementNs;
            _isRequest = isRequest;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            EnsureStreamIsOpen();
            if (buffer == null)
            {
                throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(buffer)), _message);
            }

            if (offset < 0)
            {
                throw TraceUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), offset,
                                                SRCommon.ValueMustBeNonNegative), _message);
            }

            if (count < 0)
            {
                throw TraceUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), count,
                                                SRCommon.ValueMustBeNonNegative), _message);
            }

            if (buffer.Length - offset < count)
            {
                throw TraceUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.SFxInvalidStreamOffsetLength, offset + count)), _message);
            }

            try
            {
                if (_reader == null)
                {
                    _reader = _message.GetReaderAtBodyContents();
                    if (_wrapperName != null)
                    {
                        _reader.MoveToContent();
                        _reader.ReadStartElement(_wrapperName, _wrapperNs);
                    }
                    _reader.MoveToContent();
                    if (_reader.NodeType == XmlNodeType.EndElement)
                    {
                        return 0;
                    }

                    _reader.ReadStartElement(_elementName, _elementNs);
                }
                if (_reader.MoveToContent() != XmlNodeType.Text)
                {
                    Exhaust(_reader);
                    return 0;
                }
                int bytesRead = _reader.ReadContentAsBase64(buffer, offset, count);
                _position += bytesRead;
                if (bytesRead == 0)
                {
                    Exhaust(_reader);
                }
                return bytesRead;
            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                {
                    throw;
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new IOException(SR.SFxStreamIOException, ex));
            }
        }

        private void EnsureStreamIsOpen()
        {
            if (_message.State == MessageState.Closed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(
                    _isRequest ? SR.SFxStreamRequestMessageClosed : SR.SFxStreamResponseMessageClosed));
            }
        }

        private static void Exhaust(XmlDictionaryReader reader)
        {
            if (reader != null)
            {
                while (reader.Read())
                {
                    // drain
                }
            }
        }

        public override long Position
        {
            get
            {
                EnsureStreamIsOpen();
                return _position;
            }
            set { throw TraceUtility.ThrowHelperError(new NotSupportedException(), _message); }
        }

        protected override void Dispose(bool isDisposing)
        {
            _message.Close();
            if (_reader != null)
            {
                _reader.Dispose();
                _reader = null;
            }
            base.Dispose(isDisposing);
        }

        public override bool CanRead { get { return _message.State != MessageState.Closed; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return false; } }
        public override long Length
        {
            get
            {
                throw TraceUtility.ThrowHelperError(new NotSupportedException(), _message);
            }
        }
        public override void Flush() { throw TraceUtility.ThrowHelperError(new NotSupportedException(), _message); }
        public override long Seek(long offset, SeekOrigin origin) { throw TraceUtility.ThrowHelperError(new NotSupportedException(), _message); }
        public override void SetLength(long value) { throw TraceUtility.ThrowHelperError(new NotSupportedException(), _message); }
        public override void Write(byte[] buffer, int offset, int count) { throw TraceUtility.ThrowHelperError(new NotSupportedException(), _message); }
    }
}
