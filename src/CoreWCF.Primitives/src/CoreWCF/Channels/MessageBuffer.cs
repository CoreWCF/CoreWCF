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
        private readonly XmlBuffer msgBuffer;
        private readonly KeyValuePair<string, object>[] properties;
        private readonly bool[] understoodHeaders;
        private bool closed;
        private readonly MessageVersion version;
        private readonly Uri to;
        private readonly string action;
        private readonly bool isNullMessage;

        public DefaultMessageBuffer(Message message, XmlBuffer msgBuffer)
        {
            this.msgBuffer = msgBuffer;
            version = message.Version;
            isNullMessage = message is NullMessage;

            properties = new KeyValuePair<string, object>[message.Properties.Count];
            ((ICollection<KeyValuePair<string, object>>)message.Properties).CopyTo(properties, 0);
            understoodHeaders = new bool[message.Headers.Count];
            for (int i = 0; i < understoodHeaders.Length; ++i)
            {
                understoodHeaders[i] = message.Headers.IsUnderstood(i);
            }

            //CSDMain 17837: CreateBufferedCopy should have code to copy over the To and Action headers
            if (version == MessageVersion.None)
            {
                to = message.Headers.To;
                action = message.Headers.Action;
            }
        }

        private object ThisLock
        {
            get { return msgBuffer; }
        }

        public override int BufferSize
        {
            get { return msgBuffer.BufferSize; }
        }

        public override void Close()
        {
            lock (ThisLock)
            {
                if (closed)
                {
                    return;
                }

                closed = true;
                for (int i = 0; i < properties.Length; i++)
                {
                    IDisposable disposable = properties[i].Value as IDisposable;
                    if (disposable != null)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        public override Message CreateMessage()
        {
            if (closed)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
            }

            Message msg;
            if (isNullMessage)
            {
                msg = new NullMessage();
            }
            else
            {
                msg = Message.CreateMessage(msgBuffer.GetReader(0), int.MaxValue, version);
            }

            lock (ThisLock)
            {
                msg.Properties.CopyProperties(properties);
            }

            for (int i = 0; i < understoodHeaders.Length; ++i)
            {
                if (understoodHeaders[i])
                {
                    msg.Headers.AddUnderstood(i);
                }
            }

            if (to != null)
            {
                msg.Headers.To = to;
            }

            if (action != null)
            {
                msg.Headers.Action = action;
            }

            return msg;
        }
    }

    internal class BufferedMessageBuffer : MessageBuffer
    {
        private IBufferedMessageData messageData;
        private readonly KeyValuePair<string, object>[] properties;
        private bool closed;
        private readonly object thisLock = new object();
        private readonly bool[] understoodHeaders;
        private readonly bool understoodHeadersModified;

        public BufferedMessageBuffer(IBufferedMessageData messageData,
            KeyValuePair<string, object>[] properties, bool[] understoodHeaders, bool understoodHeadersModified)
        {
            this.messageData = messageData;
            this.properties = properties;
            this.understoodHeaders = understoodHeaders;
            this.understoodHeadersModified = understoodHeadersModified;
            messageData.Open();
        }

        public override int BufferSize
        {
            get
            {
                lock (ThisLock)
                {
                    if (closed)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                    }

                    return messageData.Buffer.Count;
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
                if (closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                }

                ArraySegment<byte> buffer = messageData.Buffer;
                stream.Write(buffer.Array, buffer.Offset, buffer.Count);
            }
        }

        public override string MessageContentType
        {
            get
            {
                lock (ThisLock)
                {
                    if (closed)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                    }

                    return messageData.MessageEncoder.ContentType;
                }
            }
        }

        private object ThisLock
        {
            get { return thisLock; }
        }

        public override void Close()
        {
            lock (ThisLock)
            {
                if (!closed)
                {
                    closed = true;
                    messageData.Close();
                    messageData = null;
                }
            }
        }

        public override Message CreateMessage()
        {
            lock (ThisLock)
            {
                if (closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                }

                RecycledMessageState recycledMessageState = messageData.TakeMessageState();
                if (recycledMessageState == null)
                {
                    recycledMessageState = new RecycledMessageState();
                }

                BufferedMessage bufferedMessage = new BufferedMessage(messageData, recycledMessageState, understoodHeaders, understoodHeadersModified);
                bufferedMessage.Properties.CopyProperties(properties);
                messageData.Open();
                return bufferedMessage;
            }
        }
    }

    internal class BodyWriterMessageBuffer : MessageBuffer
    {
        private BodyWriter bodyWriter;
        private KeyValuePair<string, object>[] properties;
        private MessageHeaders headers;
        private bool closed;
        private readonly object thisLock = new object();

        public BodyWriterMessageBuffer(MessageHeaders headers,
            KeyValuePair<string, object>[] properties, BodyWriter bodyWriter)
        {
            this.bodyWriter = bodyWriter;
            this.headers = new MessageHeaders(headers);
            this.properties = properties;
        }

        protected object ThisLock
        {
            get { return thisLock; }
        }

        public override int BufferSize
        {
            get { return 0; }
        }

        public override void Close()
        {
            lock (ThisLock)
            {
                if (!closed)
                {
                    closed = true;
                    bodyWriter = null;
                    headers = null;
                    properties = null;
                }
            }
        }

        public override Message CreateMessage()
        {
            lock (ThisLock)
            {
                if (closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                }

                return new BodyWriterMessage(headers, properties, bodyWriter);
            }
        }

        protected BodyWriter BodyWriter
        {
            get { return bodyWriter; }
        }

        protected MessageHeaders Headers
        {
            get { return headers; }
        }

        protected KeyValuePair<string, object>[] Properties
        {
            get { return properties; }
        }

        protected bool Closed
        {
            get { return closed; }
        }
    }

}

