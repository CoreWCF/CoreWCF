// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;
using CoreWCF.Runtime.Diagnostics;

namespace CoreWCF.Channels
{
    internal class ByteStreamMessageEncoder : MessageEncoder, ITraceSourceStringProvider
    {
        private readonly string _maxSentMessageSizeExceededResourceString;
        private readonly XmlDictionaryReaderQuotas _quotas;
        private readonly XmlDictionaryReaderQuotas _bufferedReadReaderQuotas;

        public ByteStreamMessageEncoder(XmlDictionaryReaderQuotas quotas)
        {
            _quotas = new XmlDictionaryReaderQuotas();
            quotas.CopyTo(_quotas);

            _bufferedReadReaderQuotas = EncoderHelpers.GetBufferedReadQuotas(_quotas);

            _maxSentMessageSizeExceededResourceString = SR.MaxSentMessageSizeExceeded;
        }

        public override string ContentType => null;

        public override string MediaType => null;

        public override MessageVersion MessageVersion => MessageVersion.None;

        public override bool IsContentTypeSupported(string contentType) => true;

        public override Task<Message> ReadMessageAsync(Stream stream, int maxSizeOfHeaders, string contentType)
        {
            if (stream == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(stream));
            }

            //if (TD.ByteStreamMessageDecodingStartIsEnabled())
            //{
            //    TD.ByteStreamMessageDecodingStart();
            //}

            Message message = ByteStreamMessage.CreateMessage(stream, _quotas);
            message.Properties.Encoder = this;

            //if (SMTD.StreamedMessageReadByEncoderIsEnabled())
            //{
            //    SMTD.StreamedMessageReadByEncoder(EventTraceActivityHelper.TryExtractActivity(message, true));
            //}

            //if (MessageLogger.LogMessagesAtTransportLevel)
            //{
            //    MessageLogger.LogMessage(ref message, MessageLoggingSource.TransportReceive);
            //}

            return Task.FromResult(message);
        }

        public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
        {
            if (buffer.Array == null)
            {
                throw Fx.Exception.ArgumentNull("buffer.Array");
            }

            if (bufferManager == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(bufferManager));
            }

            //if (TD.ByteStreamMessageDecodingStartIsEnabled())
            //{
            //    TD.ByteStreamMessageDecodingStart();
            //}

            var messageData = new ByteStreamBufferedMessageData(buffer, bufferManager);

            Message message = ByteStreamMessage.CreateMessage(messageData, _bufferedReadReaderQuotas);
            message.Properties.Encoder = this;

            //if (SMTD.MessageReadByEncoderIsEnabled())
            //{
            //    SMTD.MessageReadByEncoder(
            //        EventTraceActivityHelper.TryExtractActivity(message, true),
            //        buffer.Count,
            //        this);
            //}

            //if (MessageLogger.LogMessagesAtTransportLevel)
            //{
            //    MessageLogger.LogMessage(ref message, MessageLoggingSource.TransportReceive);
            //}

            return message;
        }

        public override async Task WriteMessageAsync(Message message, Stream stream)
        {
            if (message == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(message));
            }

            if (stream == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(stream));
            }

            ThrowIfMismatchedMessageVersion(message);

            //EventTraceActivity eventTraceActivity = null;
            //if (TD.ByteStreamMessageEncodingStartIsEnabled())
            //{
            //    eventTraceActivity = EventTraceActivityHelper.TryExtractActivity(message);
            //    TD.ByteStreamMessageEncodingStart(eventTraceActivity);
            //}

            message.Properties.Encoder = this;

            //if (MessageLogger.LogMessagesAtTransportLevel)
            //{
            //    MessageLogger.LogMessage(ref message, MessageLoggingSource.TransportSend);
            //}

            using (XmlWriter writer = new XmlByteStreamWriter(stream, false))
            {
                await message.WriteMessageAsync(writer);
                await writer.FlushAsync();
            }

            //if (SMTD.StreamedMessageWrittenByEncoderIsEnabled())
            //{
            //    SMTD.StreamedMessageWrittenByEncoder(eventTraceActivity ?? EventTraceActivityHelper.TryExtractActivity(message));
            //}
        }

        public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
        {
            if (message == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(message));
            }

            if (bufferManager == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(bufferManager));
            }

            if (maxMessageSize < 0)
            {
                throw Fx.Exception.ArgumentOutOfRange(nameof(maxMessageSize), maxMessageSize, SR.Format(SR.ArgumentOutOfMinRange, 0));
            }

            if (messageOffset < 0)
            {
                throw Fx.Exception.ArgumentOutOfRange(nameof(messageOffset), messageOffset, SR.Format(SR.ArgumentOutOfMinRange, 0));
            }

            //EventTraceActivity eventTraceActivity = null;
            //if (TD.ByteStreamMessageEncodingStartIsEnabled())
            //{
            //    eventTraceActivity = EventTraceActivityHelper.TryExtractActivity(message);
            //    TD.ByteStreamMessageEncodingStart(eventTraceActivity);
            //}

            ThrowIfMismatchedMessageVersion(message);
            message.Properties.Encoder = this;

            ArraySegment<byte> messageBuffer;

            using (BufferManagerOutputStream stream = new BufferManagerOutputStream(_maxSentMessageSizeExceededResourceString, 0, maxMessageSize, bufferManager))
            {
                stream.Skip(messageOffset);
                using (XmlWriter writer = new XmlByteStreamWriter(stream, true))
                {
                    message.WriteMessage(writer);
                    writer.Flush();
                    byte[] bytes = stream.ToArray(out int size);
                    messageBuffer = new ArraySegment<byte>(bytes, messageOffset, size - messageOffset);
                }
            }

            //if (SMTD.MessageWrittenByEncoderIsEnabled())
            //{
            //    SMTD.MessageWrittenByEncoder(
            //        eventTraceActivity ?? EventTraceActivityHelper.TryExtractActivity(message),
            //        messageBuffer.Count,
            //        this);
            //}

            //if (MessageLogger.LogMessagesAtTransportLevel)
            //{
            //    // DevDiv#486728
            //    // Don't pass in a buffer manager to avoid returning 'messageBuffer" to the bufferManager twice.
            //    ByteStreamBufferedMessageData messageData = new ByteStreamBufferedMessageData(messageBuffer, null);
            //    using (XmlReader reader = new XmlBufferedByteStreamReader(messageData, this._quotas))
            //    {
            //        MessageLogger.LogMessage(ref message, reader, MessageLoggingSource.TransportSend);
            //    }
            //}

            return messageBuffer;
        }

        public override string ToString() => ByteStreamMessageUtility.EncoderName;

        public Stream GetResponseMessageStream(Message message)
        {
            if (message == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(message));
            }

            ThrowIfMismatchedMessageVersion(message);

            if (!ByteStreamMessage.IsInternalByteStreamMessage(message))
            {
                return null;
            }

            return message.GetBody<Stream>();
        }

        string ITraceSourceStringProvider.GetSourceString()
        {
            // Other MessageEncoders use base.GetTraceSourceString but that would require a public api change in MessageEncoder
            // as ByteStreamMessageEncoder is in a different assemly. The same logic is reimplemented here.
            //if (_traceSourceString == null)
            //{
            //    _traceSourceString = DiagnosticTraceBase.CreateDefaultSourceString(this);
            //}

            return null;
        }
    }
}
