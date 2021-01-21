// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels.Framing
{
    internal class StreamedFramingRequestContext : RequestContextBase
    {
        private FramingConnection _connection;
        private Message _requestMessage;
        private Stream _inputStream;
        bool isClosed;
        private TaskCompletionSource<object> _tcs;

        public StreamedFramingRequestContext(FramingConnection connection, Message requestMessage, Stream inputStream)
            : base(requestMessage, connection.ServiceDispatcher.Binding.CloseTimeout, connection.ServiceDispatcher.Binding.SendTimeout)
        {
            _connection = connection;
            _requestMessage = requestMessage;
            _inputStream = inputStream;
            _tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        protected override void OnAbort()
        {
            _tcs.TrySetResult(null);
            _connection.Abort();
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            lock (ThisLock)
            {
                if (isClosed)
                {
                    return;
                }

                isClosed = true;
            }

            bool success = false;
            try
            {
                // first drain our stream if necessary
                if (_inputStream != null)
                {
                    byte[] dummy = Fx.AllocateByteArray(_connection.ConnectionBufferSize);
                    while (!_connection.EOF)
                    {
                        int bytesRead = await _inputStream.ReadAsync(dummy, 0, dummy.Length, token);
                        if (bytesRead == 0)
                        {
                            _connection.EOF = true;
                        }
                    }
                }

                // send back EOF and then recycle the connection
                _connection.RawStream?.StartUnwrapRead();
                try
                {
                    await _connection.Output.WriteAsync(SingletonEncoder.EndBytes, token);
                    await _connection.Output.FlushAsync(token);
                }
                finally
                {
                    if (_connection.RawStream != null)
                    {
                        await _connection.RawStream.FinishUnwrapReadAsync();
                        _connection.RawStream = null;
                        _connection.Output.Complete();
                        _connection.Input.Complete();
                    }
                }

                // TODO: ChannelBinding
                //ChannelBindingUtility.Dispose(ref this.channelBindingToken);

                success = true;
            }
            finally
            {
                _tcs.TrySetResult(null);

                if (!success)
                {
                    Abort();
                }
            }
        }

        protected override async Task OnReplyAsync(Message message, CancellationToken token)
        {
            ICompressedMessageEncoder compressedMessageEncoder = _connection.MessageEncoderFactory.Encoder as ICompressedMessageEncoder;
            if (compressedMessageEncoder != null && compressedMessageEncoder.CompressionEnabled)
            {
                compressedMessageEncoder.AddCompressedMessageProperties(message, _connection.FramingDecoder.ContentType);
            }

            await StreamingConnectionHelper.WriteMessageAsync(message, _connection, false, _connection.ServiceDispatcher.Binding, token);
        }

        public Task ReplySent => _tcs.Task;
    }

    static class StreamingConnectionHelper
    {
        public static async Task WriteMessageAsync(Message message, FramingConnection connection, bool isRequest,
            IDefaultCommunicationTimeouts settings, CancellationToken token)
        {
            byte[] endBytes = null;
            if (message != null)
            {
                MessageEncoder messageEncoder = connection.MessageEncoderFactory.Encoder;
                byte[] envelopeStartBytes = SingletonEncoder.EnvelopeStartBytes;

                bool writeStreamed;
                if (isRequest)
                {
                    endBytes = SingletonEncoder.EnvelopeEndFramingEndBytes;
                    writeStreamed = TransferModeHelper.IsRequestStreamed(connection.TransferMode);
                }
                else
                {
                    endBytes = SingletonEncoder.EnvelopeEndBytes;
                    writeStreamed = TransferModeHelper.IsResponseStreamed(connection.TransferMode);
                }

                if (writeStreamed)
                {
                    await connection.Output.WriteAsync(envelopeStartBytes, token);
                    Stream connectionStream = new StreamingOutputConnectionStream(connection, settings);
                    // TODO: Determine if timeout stream is needed as StreamingOutputConnectionStream implements some timeout functionality
                    //Stream writeTimeoutStream = new TimeoutStream(connectionStream, ref timeoutHelper);
                    messageEncoder.WriteMessageAsync(message, connectionStream);
                    await connection.Output.FlushAsync();
                }
                else
                {
                    ArraySegment<byte> messageData = messageEncoder.WriteMessage(message,
                        int.MaxValue, connection.BufferManager, envelopeStartBytes.Length + IntEncoder.MaxEncodedSize);
                    messageData = SingletonEncoder.EncodeMessageFrame(messageData);
                    Buffer.BlockCopy(envelopeStartBytes, 0, messageData.Array, messageData.Offset - envelopeStartBytes.Length,
                        envelopeStartBytes.Length);
                    await connection.Output.WriteAsync(new ArraySegment<byte>(messageData.Array, messageData.Offset - envelopeStartBytes.Length,
                        messageData.Count + envelopeStartBytes.Length), token);
                    await connection.Output.FlushAsync();
                    connection.BufferManager.ReturnBuffer(messageData.Array);
                }
            }
            else if (isRequest) // context handles response end bytes
            {
                endBytes = SingletonEncoder.EndBytes;
            }

            if (endBytes != null)
            {
                await connection.Output.WriteAsync(endBytes, token);
                await connection.Output.FlushAsync();
            }
        }
    }

    // overrides Stream to add a Framing int at the beginning of each record
    class StreamingOutputConnectionStream : Stream
    {
        byte[] _encodedSize;
        private FramingConnection _connection;
        private IDefaultCommunicationTimeouts _timeouts;

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));

        public override long Position
        {
            get => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
            set => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
        }

        public StreamingOutputConnectionStream(FramingConnection connection, IDefaultCommunicationTimeouts timeouts)
        {
            _encodedSize = new byte[IntEncoder.MaxEncodedSize];
            _connection = connection;
            _timeouts = timeouts;
        }

        private async Task WriteChunkSizeAsync(int size, CancellationToken token)
        {
            if (size > 0)
            {
                int bytesEncoded = IntEncoder.Encode(size, _encodedSize, 0);
                await _connection.Output.WriteAsync(new ArraySegment<byte>(_encodedSize, 0, bytesEncoded), token);
            }
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return WriteAsync(buffer, offset, count, CancellationToken.None).ToApm(callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            asyncResult.ToApmEnd();
        }

        public override void WriteByte(byte value)
        {
            var timeoutHelper = new TimeoutHelper(_timeouts.SendTimeout);
            var ct = timeoutHelper.GetCancellationToken();
            WriteChunkSizeAsync(1, ct).GetAwaiter().GetResult();
            _connection.Output.WriteAsync(new byte[] { value }, ct).GetAwaiter().GetResult();
            _connection.Output.FlushAsync();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var timeoutHelper = new TimeoutHelper(_timeouts.SendTimeout);
            var ct = timeoutHelper.GetCancellationToken();
            await WriteChunkSizeAsync(count, ct);
            await _connection.Output.WriteAsync(new ArraySegment<byte>(buffer, offset, count), ct);
            await _connection.Output.FlushAsync();
        }

        public override void Flush()
        {
            _connection.Output.FlushAsync().GetAwaiter().GetResult();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
        }

        public override void SetLength(long value)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
        }
    }
}