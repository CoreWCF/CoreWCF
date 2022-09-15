// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Xml;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoreWCF.Channels
{
    public static class MsmqDecodeHelper
    {
        private const int DefaultMaxViaSize = 2048;
        private const int DefaultMaxContentTypeSize = 256;

        public async static void DecodeTransportDatagram(PipeReader pipeReader)
        {
            var serverModeDecoder = new ServerModeDecoder( NullLogger.Instance);
            await serverModeDecoder.ReadModeAsync(pipeReader);
            var decoder = new ServerSingletonSizedDecoder(DefaultMaxViaSize, DefaultMaxContentTypeSize,NullLogger.Instance);
            var readResult = await pipeReader.ReadAsync();
            if (readResult.IsCompleted)
            {
                return;
            }

            var buffer = readResult.Buffer;

           try
            {
                do
                {
                    if (buffer.Length <= 0)
                    {
                        throw decoder.CreatePrematureEOFException();
                    }
                    int decoded = decoder.Decode(buffer);
                    buffer = buffer.Slice(decoded);
                } while (decoder.CurrentState != ServerSingletonSizedDecoder.State.Start);
				pipeReader.AdvanceTo(buffer.Start);
            }
            catch (ProtocolException ex)
            {
                throw new MsmqPoisonMessageException(0, ex);
            }
        }
        /*


        public static Message DecodeTransportDatagram1(Stream stream, MessageEncoder encoder, int maxReceivedMessageSize)
        {
            var bufferManager = BufferManager.CreateBufferManager(16, int.MaxValue);

            int size = (int)stream.Length;
            int offset = 0;

            long lookupId = 0; // todo read from?
            byte[] incoming = new byte[size];
            stream.Read(incoming, 0, size);

            var modeDecoder = new ServerModeDecoder();

            try
            {
                ReadServerMode(modeDecoder, incoming, ref offset, ref size);
            }
            catch (ProtocolException ex)
            {
                throw new MsmqPoisonMessageException(lookupId, ex);
            }

            if (modeDecoder.Mode != FramingMode.SingletonSized)
            {
                throw new MsmqPoisonMessageException(lookupId, new ProtocolException(SR.MsmqBadFrame));
            }

            var decoder = new ServerSingletonSizedDecoder(0, DefaultMaxViaSize, DefaultMaxContentTypeSize);
            try
            {
                do
                {
                    if (size <= 0)
                    {
                        throw decoder.CreatePrematureEOFException();
                    }

                    int decoded = decoder.Decode(incoming, offset, size);
                    offset += decoded;
                    size -= decoded;
                } while (decoder.CurrentState != ServerSingletonSizedDecoder.State.Start);
            }
            catch (ProtocolException ex)
            {
                throw new MsmqPoisonMessageException(lookupId, ex);
            }

            if (size > maxReceivedMessageSize)
            {
                throw new MsmqPoisonMessageException(lookupId, MaxMessageSizeStream.CreateMaxReceivedMessageSizeExceededException(size));
            }

            if (!encoder.IsContentTypeSupported(decoder.ContentType))
            {
                throw new MsmqPoisonMessageException(lookupId, new ProtocolException(SR.MsmqBadContentType));
            }

            byte[] envelopeBuffer = bufferManager.TakeBuffer(size);
            Buffer.BlockCopy(incoming, offset, envelopeBuffer, 0, size);

            Message message;

            try
            {
                message = encoder.ReadMessage(new ArraySegment<byte>(envelopeBuffer, 0, size), bufferManager);
            }
            catch (XmlException e)
            {
                throw new MsmqPoisonMessageException(lookupId, new ProtocolException(SR.Format(SR.MsmqBadXml, e)));
            }

            return message;
        }

       
        private static void ReadServerMode(ServerModeDecoder modeDecoder, byte[] incoming, ref int offset, ref int size)
        {
            do
            {
                if (size <= 0)
                {
                    throw modeDecoder.CreatePrematureEOFException();
                }

                int decoded = modeDecoder.Decode1(incoming, offset, size);
                offset += decoded;
                size -= decoded;
            } while (ServerModeDecoder.State.Done != modeDecoder.CurrentState);
        }*/
    }
}
