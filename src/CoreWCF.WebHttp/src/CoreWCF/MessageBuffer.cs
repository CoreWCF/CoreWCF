// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using CoreWCF.Channels;

namespace CoreWCF
{
    internal class BufferedMessageBuffer : MessageBuffer
    {
        private IBufferedMessageData2 _messageData;
        private readonly KeyValuePair<string, object>[] _properties;
        private bool _closed;
        private readonly bool[] _understoodHeaders;
        private readonly bool _understoodHeadersModified;

        public BufferedMessageBuffer(IBufferedMessageData2 messageData,
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

                    return (int)_messageData.ReadOnlyBuffer.Length;
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

                foreach (var memory in _messageData.ReadOnlyBuffer)
                {
                    stream.Write(memory);
                }
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
                foreach (KeyValuePair<string, object> keypair in _properties)
                {
                    bufferedMessage.Properties[keypair.Key] = keypair.Value;
                }
                _messageData.Open();
                return bufferedMessage;
            }
        }

        private Exception CreateBufferDisposedException()
        {
            return new ObjectDisposedException("", SR.MessageBufferIsClosed);
        }
    }
}
