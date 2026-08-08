// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    /// <summary>
    /// Faults as soon as a message larger than <c>maxMessageSize</c> has come through, so a reader
    /// stops paying for a body that is already too big instead of taking all of it and deciding
    /// afterwards.
    /// </summary>
    /// <remarks>
    /// The size counted is everything consumed so far plus whatever is currently buffered, which
    /// covers both ways a message gets read off a pipe: buffered, where nothing is consumed until
    /// the whole message has arrived, and streamed, where the buffer stays small because the reader
    /// consumes as it goes.
    /// </remarks>
    internal sealed class MaxMessageSizePipeReader : PipeReader
    {
        private readonly PipeReader _reader;
        private readonly long _maxMessageSize;
        private readonly Func<long, Exception> _createException;
        private ReadOnlySequence<byte> _currentBuffer;
        private long _totalConsumed;

        public MaxMessageSizePipeReader(PipeReader reader, long maxMessageSize)
            : this(reader, maxMessageSize, MaxMessageSizeStream.CreateMaxReceivedMessageSizeExceededException)
        {
        }

        /// <param name="createException">
        /// Builds the exception to fault with, given the size that was exceeded. Transports answer
        /// this differently: an HTTP request replies 413, for instance.
        /// </param>
        public MaxMessageSizePipeReader(PipeReader reader, long maxMessageSize, Func<long, Exception> createException)
        {
            _reader = reader ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            _createException = createException ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(createException));
            _maxMessageSize = maxMessageSize;
        }

        public override void AdvanceTo(SequencePosition consumed)
        {
            Consume(consumed);
            _reader.AdvanceTo(consumed);
        }

        public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
        {
            Consume(consumed);
            _reader.AdvanceTo(consumed, examined);
        }

        public override void CancelPendingRead() => _reader.CancelPendingRead();

        public override void Complete(Exception exception = null) => _reader.Complete(exception);

        public override ValueTask CompleteAsync(Exception exception = null) => _reader.CompleteAsync(exception);

        public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            ReadResult result = await _reader.ReadAsync(cancellationToken);
            OnRead(result);
            return result;
        }

        public override bool TryRead(out ReadResult result)
        {
            if (!_reader.TryRead(out result))
            {
                return false;
            }

            OnRead(result);
            return true;
        }

        private void OnRead(ReadResult result)
        {
            _currentBuffer = result.Buffer;

            if (_totalConsumed + result.Buffer.Length > _maxMessageSize)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(_createException(_maxMessageSize));
            }
        }

        private void Consume(SequencePosition consumed)
        {
            _totalConsumed += _currentBuffer.Slice(_currentBuffer.Start, consumed).Length;
            _currentBuffer = default;
        }
    }
}
