// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public abstract class MessageEncoder
    {
        public abstract string ContentType { get; }

        public abstract string MediaType { get; }

        public abstract MessageVersion MessageVersion { get; }

        public virtual T GetProperty<T>() where T : class
        {
            if (typeof(T) == typeof(FaultConverter))
            {
                return (T)(object)FaultConverter.GetDefaultFaultConverter(MessageVersion);
            }

            return null;
        }

        public Task<Message> ReadMessageAsync(Stream stream, int maxSizeOfHeaders)
        {
            return ReadMessageAsync(stream, maxSizeOfHeaders, null);
        }

        public abstract Task<Message> ReadMessageAsync(Stream stream, int maxSizeOfHeaders, string contentType);

        [Obsolete("Callers should use ReadMessageAsync(ReadOnlySequence<byte> buffer, MemoryPool<byte> memoryPool, string contentType) instead.")]
        public Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager)
        {
            return ReadMessage(buffer, bufferManager, null);
        }

        [Obsolete("Implementers should override ReadMessageAsync(ReadOnlySequence<byte> buffer, MemoryPool<byte> memoryPool, string contentType).")]
        public virtual Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
        {
            return ReadMessageAsync(new ReadOnlySequence<byte>(buffer), bufferManager, contentType).AsTask().GetAwaiter().GetResult();
        }

        public ValueTask<Message> ReadMessageAsync(ReadOnlySequence<byte> buffer, MemoryPool<byte> memoryPool) => ReadMessageAsync(buffer, memoryPool, contentType: null);

        // Default to forward the call to ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
        // to support derived type implementations
        public virtual ValueTask<Message> ReadMessageAsync(ReadOnlySequence<byte> buffer, MemoryPool<byte> memoryPool, string contentType)
        {
            int bufferLength = (int)buffer.Length;
            BufferManager bufferManager = memoryPool;
            byte[] bytes = bufferManager.TakeBuffer(bufferLength);
            buffer.CopyTo(bytes.AsSpan(0, bufferLength));
#pragma warning disable CS0612
            Message message = ReadMessage(new ArraySegment<byte>(bytes, 0, bufferLength), bufferManager, contentType);
#pragma warning restore CS0612
            return new ValueTask<Message>(message);
        }

        public override string ToString()
        {
            return ContentType;
        }

        public abstract Task WriteMessageAsync(Message message, Stream stream);

        public ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager)
        {
            ArraySegment<byte> arraySegment = WriteMessage(message, maxMessageSize, bufferManager, 0);
            return arraySegment;
        }

        public abstract ArraySegment<byte> WriteMessage(Message message, int maxMessageSize,
            BufferManager bufferManager, int messageOffset);

        public virtual bool IsContentTypeSupported(string contentType)
        {
            if (contentType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(contentType)));
            }

            return IsContentTypeSupported(contentType, ContentType, MediaType);
        }

        protected bool IsContentTypeSupported(string contentType, string supportedContentType, string supportedMediaType)
        {
            if (supportedContentType == contentType)
            {
                return true;
            }

            if (contentType.Length > supportedContentType.Length &&
                contentType.StartsWith(supportedContentType, StringComparison.Ordinal) &&
                contentType[supportedContentType.Length] == ';')
            {
                return true;
            }

            // now check case-insensitively
            if (contentType.StartsWith(supportedContentType, StringComparison.OrdinalIgnoreCase))
            {
                if (contentType.Length == supportedContentType.Length)
                {
                    return true;
                }
                else if (contentType.Length > supportedContentType.Length)
                {
                    char ch = contentType[supportedContentType.Length];

                    // Linear Whitespace is allowed to appear between the end of one property and the semicolon.
                    // LWS = [CRLF]? (SP | HT)+
                    if (ch == ';')
                    {
                        return true;
                    }

                    // Consume the [CRLF]?
                    int i = supportedContentType.Length;
                    if (ch == '\r' && contentType.Length > supportedContentType.Length + 1 && contentType[i + 1] == '\n')
                    {
                        i += 2;
                        ch = contentType[i];
                    }

                    // Look for a ';' or nothing after (SP | HT)+
                    if (ch == ' ' || ch == '\t')
                    {
                        i++;
                        while (i < contentType.Length)
                        {
                            ch = contentType[i];
                            if (ch != ' ' && ch != '\t')
                            {
                                break;
                            }

                            ++i;
                        }
                    }
                    if (ch == ';' || i == contentType.Length)
                    {
                        return true;
                    }
                }
            }

            // sometimes we get a contentType that has parameters, but our encoders
            // merely expose the base content-type, so we will check a stripped version
            try
            {
                MediaTypeHeaderValue parsedContentType = MediaTypeHeaderValue.Parse(contentType);

                if (supportedMediaType.Length > 0 && !supportedMediaType.Equals(parsedContentType.MediaType, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!IsCharSetSupported(parsedContentType.CharSet))
                {
                    return false;
                }
            }
            catch (FormatException)
            {
                // bad content type, so we definitely don't support it!
                return false;
            }

            return true;
        }

        protected virtual bool IsCharSetSupported(string charset)
        {
            return false;
        }

        protected void ThrowIfMismatchedMessageVersion(Message message)
        {
            if (message.Version != MessageVersion)
            {
                throw TraceUtility.ThrowHelperError(
                    new ProtocolException(SR.Format(SR.EncoderMessageVersionMismatch, message.Version, MessageVersion)),
                    message);
            }
        }

        internal string GetTraceSourceString()
        {
            // if (_traceSourceString == null)
            // {
            //     _traceSourceString = Runtime.Diagnostics.DiagnosticTraceBase.CreateDefaultSourceString(this);
            // }

            return null;
        }
    }
}
