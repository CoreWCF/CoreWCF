// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class HttpStreamMessage : Message
    {
        internal const string StreamElementName = "Binary";
        private BodyWriter _bodyWriter;
        private readonly MessageHeaders _headers;
        private readonly MessageProperties _properties;

        public HttpStreamMessage(BodyWriter writer)
        {
            _bodyWriter = writer ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            _headers = new MessageHeaders(MessageVersion.None, 1);
            _properties = new MessageProperties();
        }

        public HttpStreamMessage(MessageHeaders headers, MessageProperties properties, BodyWriter bodyWriter)
        {
            _headers = new MessageHeaders(headers);
            _properties = new MessageProperties(properties);
            _bodyWriter = bodyWriter ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bodyWriter));
        }

        public override MessageHeaders Headers
        {
            get
            {
                if (IsDisposed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateDisposedException());
                }

                return _headers;
            }
        }

        public override bool IsEmpty => false;

        public override bool IsFault => false;

        public override MessageProperties Properties
        {
            get
            {
                if (IsDisposed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateDisposedException());
                }

                return _properties;
            }
        }

        public override MessageVersion Version
        {
            get
            {
                if (IsDisposed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateDisposedException());
                }

                return MessageVersion.None;
            }
        }

        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            if (_bodyWriter.IsBuffered)
            {
                _bodyWriter.WriteBodyContents(writer);
            }
            else
            {
                writer.WriteString(SR.Format(SR.MessageBodyIsStream));
            }
        }

        protected override void OnClose()
        {
            Exception ex = null;
            try
            {
                base.OnClose();
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                ex = e;
            }

            try
            {
                if (_properties != null)
                {
                    _properties.Dispose();
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                if (ex == null)
                {
                    ex = e;
                }
            }

            if (ex != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(ex);
            }

            _bodyWriter = null;
        }

        protected override MessageBuffer OnCreateBufferedCopy(int maxBufferSize)
        {
            BodyWriter bufferedBodyWriter;
            if (_bodyWriter.IsBuffered)
            {
                bufferedBodyWriter = _bodyWriter;
            }
            else
            {
                bufferedBodyWriter = _bodyWriter.CreateBufferedCopy(maxBufferSize);
            }

            return new HttpStreamMessageBuffer(Headers, new MessageProperties(Properties), bufferedBodyWriter);
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            _bodyWriter.WriteBodyContents(writer);
        }

        private Exception CreateDisposedException() => new ObjectDisposedException("", SR.Format(SR.MessageClosed));

        internal class HttpStreamMessageBuffer : MessageBuffer
        {
            private BodyWriter _bodyWriter;
            private bool _closed;
            private MessageHeaders _headers;
            private MessageProperties _properties;

            public HttpStreamMessageBuffer(MessageHeaders headers,
                MessageProperties properties, BodyWriter bodyWriter)
                : base()
            {
                _bodyWriter = bodyWriter;
                _headers = headers;
                _properties = properties;
            }

            public override int BufferSize => 0;

            private object ThisLock { get; } = new object();

            public override void Close()
            {
                lock (ThisLock)
                {
                    if (!_closed)
                    {
                        _closed = true;
                        _bodyWriter = null;
                        _headers = null;
                        _properties = null;
                    }
                }
            }

            public override Message CreateMessage()
            {
                lock (ThisLock)
                {
                    if (_closed)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateDisposedException());
                    }
                    return new HttpStreamMessage(_headers, _properties, _bodyWriter);
                }
            }

            private Exception CreateDisposedException()
            {
                return new ObjectDisposedException("", SR.Format(SR.MessageBufferIsClosed));
            }
        }
    }
}
