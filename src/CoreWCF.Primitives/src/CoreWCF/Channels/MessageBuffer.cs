// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace CoreWCF.Channels
{
    public abstract class MessageBuffer : System.IDisposable
    {
        public abstract int BufferSize { get; }

        void IDisposable.Dispose()
        {
            Close();
        }

        public abstract void Close();

        public virtual void WriteMessage(Stream stream)
        {
            if (stream == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            }

            Message message = CreateMessage();
            using (message)
            {
                XmlDictionaryWriter writer = XmlDictionaryWriter.CreateBinaryWriter(stream, XD.Dictionary, null, false);
                using (writer)
                {
                    message.WriteMessage(writer);
                }
            }
        }

        public virtual string MessageContentType
        {
            get { return FramingEncodingString.Binary; }
        }

        public abstract Message CreateMessage();

        internal Exception CreateBufferDisposedException()
        {
            return new ObjectDisposedException("", SR.MessageBufferIsClosed);
        }

        //public XPathNavigator CreateNavigator()
        //{
        //    return CreateNavigator(int.MaxValue, XmlSpace.None);
        //}

        //public XPathNavigator CreateNavigator(int nodeQuota)
        //{
        //    return CreateNavigator(nodeQuota, XmlSpace.None);
        //}

        //public XPathNavigator CreateNavigator(XmlSpace space)
        //{
        //    return CreateNavigator(int.MaxValue, space);
        //}

        //public XPathNavigator CreateNavigator(int nodeQuota, XmlSpace space)
        //{
        //    if (nodeQuota <= 0)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("nodeQuota", SR.Format(SR.FilterQuotaRange)));

        //    return new SeekableMessageNavigator(this.CreateMessage(), nodeQuota, space, true, true);
        //}
    }


    internal class DefaultMessageBuffer : MessageBuffer
    {
        private readonly XmlBuffer _msgBuffer;
        private readonly KeyValuePair<string, object>[] _properties;
        private readonly bool[] _understoodHeaders;
        private bool _closed;
        private readonly MessageVersion _version;
        private readonly Uri _to;
        private readonly string _action;
        private readonly bool _isNullMessage;

        public DefaultMessageBuffer(Message message, XmlBuffer msgBuffer)
        {
            _msgBuffer = msgBuffer;
            _version = message.Version;
            _isNullMessage = message is NullMessage;

            _properties = new KeyValuePair<string, object>[message.Properties.Count];
            ((ICollection<KeyValuePair<string, object>>)message.Properties).CopyTo(_properties, 0);
            _understoodHeaders = new bool[message.Headers.Count];
            for (int i = 0; i < _understoodHeaders.Length; ++i)
            {
                _understoodHeaders[i] = message.Headers.IsUnderstood(i);
            }

            //CSDMain 17837: CreateBufferedCopy should have code to copy over the To and Action headers
            if (_version == MessageVersion.None)
            {
                _to = message.Headers.To;
                _action = message.Headers.Action;
            }
        }

        private object ThisLock
        {
            get { return _msgBuffer; }
        }

        public override int BufferSize
        {
            get { return _msgBuffer.BufferSize; }
        }

        public override void Close()
        {
            lock (ThisLock)
            {
                if (_closed)
                {
                    return;
                }

                _closed = true;
                for (int i = 0; i < _properties.Length; i++)
                {
                    IDisposable disposable = _properties[i].Value as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        public override Message CreateMessage()
        {
            if (_closed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
            }

            Message msg;
            if (_isNullMessage)
            {
                msg = new NullMessage();
            }
            else
            {
                msg = Message.CreateMessage(_msgBuffer.GetReader(0), int.MaxValue, _version);
            }

            lock (ThisLock)
            {
                msg.Properties.CopyProperties(_properties);
            }

            for (int i = 0; i < _understoodHeaders.Length; ++i)
            {
                if (_understoodHeaders[i])
                {
                    msg.Headers.AddUnderstood(i);
                }
            }

            if (_to != null)
            {
                msg.Headers.To = _to;
            }

            if (_action != null)
            {
                msg.Headers.Action = _action;
            }

            return msg;
        }
    }

    internal class BufferedMessageBuffer : MessageBuffer
    {
        private IBufferedMessageData _messageData;
        private readonly KeyValuePair<string, object>[] _properties;
        private bool _closed;
        private readonly bool[] _understoodHeaders;
        private readonly bool _understoodHeadersModified;

        public BufferedMessageBuffer(IBufferedMessageData messageData,
            KeyValuePair<string, object>[] properties, bool[] understoodHeaders, bool understoodHeadersModified)
        {
            _messageData = messageData;
            _properties = properties;
            _understoodHeaders = understoodHeaders;
            _understoodHeadersModified = understoodHeadersModified;
            messageData.Open();
        }

        public override int BufferSize
        {
            get
            {
                lock (ThisLock)
                {
                    if (_closed)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                    }

                    return _messageData.Buffer.Count;
                }
            }
        }

        public override void WriteMessage(Stream stream)
        {
            if (stream == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            }

            lock (ThisLock)
            {
                if (_closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                }

                ArraySegment<byte> buffer = _messageData.Buffer;
                stream.Write(buffer.Array, buffer.Offset, buffer.Count);
            }
        }

        public override string MessageContentType
        {
            get
            {
                lock (ThisLock)
                {
                    if (_closed)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                    }

                    return _messageData.MessageEncoder.ContentType;
                }
            }
        }

        private object ThisLock { get; } = new object();

        public override void Close()
        {
            lock (ThisLock)
            {
                if (!_closed)
                {
                    _closed = true;
                    _messageData.Close();
                    _messageData = null;
                }
            }
        }

        public override Message CreateMessage()
        {
            lock (ThisLock)
            {
                if (_closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                }

                RecycledMessageState recycledMessageState = _messageData.TakeMessageState();
                if (recycledMessageState == null)
                {
                    recycledMessageState = new RecycledMessageState();
                }

                BufferedMessage bufferedMessage = new BufferedMessage(_messageData, recycledMessageState, _understoodHeaders, _understoodHeadersModified);
                bufferedMessage.Properties.CopyProperties(_properties);
                _messageData.Open();
                return bufferedMessage;
            }
        }
    }

    internal class BodyWriterMessageBuffer : MessageBuffer
    {
        private readonly object _thisLock = new object();

        public BodyWriterMessageBuffer(MessageHeaders headers,
            KeyValuePair<string, object>[] properties, BodyWriter bodyWriter)
        {
            BodyWriter = bodyWriter;
            Headers = new MessageHeaders(headers);
            Properties = properties;
        }

        protected object ThisLock
        {
            get { return _thisLock; }
        }

        public override int BufferSize
        {
            get { return 0; }
        }

        public override void Close()
        {
            lock (ThisLock)
            {
                if (!Closed)
                {
                    Closed = true;
                    BodyWriter = null;
                    Headers = null;
                    Properties = null;
                }
            }
        }

        public override Message CreateMessage()
        {
            lock (ThisLock)
            {
                if (Closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                }

                return new BodyWriterMessage(Headers, Properties, BodyWriter);
            }
        }

        protected BodyWriter BodyWriter { get; private set; }

        protected MessageHeaders Headers { get; private set; }

        protected KeyValuePair<string, object>[] Properties { get; private set; }

        protected bool Closed { get; private set; }
    }
}

