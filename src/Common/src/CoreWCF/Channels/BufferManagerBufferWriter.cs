// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;

namespace CoreWCF.Channels
{
    /// <summary>
    /// An <see cref="IBufferWriter{T}"/> over a <see cref="BufferManager"/>, so buffered write and read
    /// paths can be expressed with <see cref="Span{T}"/>/<see cref="Memory{T}"/> while the pool the
    /// transport was configured with stays in charge of the memory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Single use. There is no <c>Clear</c>/<c>Reinitialize</c>; create one per buffered operation.
    /// </para>
    /// <para>
    /// The instance is in exactly one of three states:
    /// <list type="bullet">
    /// <item><description><b>Owning</b> &#8212; it holds a pooled array and owes
    /// <see cref="BufferManager.ReturnBuffer"/> to the manager it rented from.</description></item>
    /// <item><description><b>Detached</b> &#8212; <see cref="DetachBuffer"/> transferred that debt to the
    /// caller.</description></item>
    /// <item><description><b>Disposed</b> &#8212; the array went back to the pool; nobody owes anything.</description></item>
    /// </list>
    /// The state is carried by the array field itself rather than by a flag, so returning the same array
    /// twice is impossible by construction. Every member except <see cref="Dispose"/> throws once the
    /// instance leaves the Owning state.
    /// </para>
    /// <para>
    /// This file is compiled into every CoreWCF assembly through the <c>$(CommonPath)</c> glob in
    /// <c>Directory.Build.targets</c>, and is also compiled directly into CoreWCF.Primitives.Tests. It must
    /// therefore depend on nothing beyond <c>System</c>, <c>System.Buffers</c> and
    /// <see cref="BufferManager"/> &#8212; in particular no <c>SR</c>/<c>SRCommon</c> (test projects build with
    /// <c>OmitResources</c>), no <c>Fx</c>, and no <c>ArrayPool</c>. That is what the injected
    /// <c>createQuotaExceededException</c> factory buys.
    /// </para>
    /// </remarks>
    internal sealed class BufferManagerBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private readonly BufferManager _bufferManager;
        private readonly int _maxSizeQuota;
        private readonly Func<int, Exception> _createQuotaExceededException;
        private byte[] _buffer;
        private int _capacity;
        private int _writtenCount;
        private bool _detached;

        /// <param name="bufferManager">The pool to rent from and return to. Never replaced by
        /// <see cref="ArrayPool{T}"/>: honouring the transport's configured <c>MaxBufferPoolSize</c> is the
        /// point of this type.</param>
        /// <param name="initialSize">Size requested from <paramref name="bufferManager"/> up front. The pool
        /// may hand back a larger array; the extra is used.</param>
        /// <param name="maxSizeQuota">The quota value reported in the exception message. This is what the
        /// user configured (for example <c>MaxReceivedMessageSize</c>).</param>
        /// <param name="effectiveMaxSize">The byte count actually enforced. It differs from
        /// <paramref name="maxSizeQuota"/> when a caller reserves a prefix &#8212; see
        /// <c>BufferedMessageWriter</c>, which enforces <c>maxSizeQuota + initialOffset</c> while still
        /// reporting <c>maxSizeQuota</c>.</param>
        /// <param name="createQuotaExceededException">Builds the exception thrown when
        /// <paramref name="effectiveMaxSize"/> would be exceeded; it is handed
        /// <paramref name="maxSizeQuota"/>. Injected so this type stays free of <c>SR</c> and of the
        /// diagnostics helpers, which differ per assembly.</param>
        internal BufferManagerBufferWriter(BufferManager bufferManager, int initialSize, int maxSizeQuota,
            int effectiveMaxSize, Func<int, Exception> createQuotaExceededException)
        {
            if (bufferManager == null)
            {
                throw new ArgumentNullException(nameof(bufferManager));
            }

            if (initialSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialSize), initialSize, "Value must be non-negative.");
            }

            if (maxSizeQuota < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSizeQuota), maxSizeQuota, "Value must be non-negative.");
            }

            if (effectiveMaxSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(effectiveMaxSize), effectiveMaxSize, "Value must be non-negative.");
            }

            if (createQuotaExceededException == null)
            {
                throw new ArgumentNullException(nameof(createQuotaExceededException));
            }

            _bufferManager = bufferManager;
            _maxSizeQuota = maxSizeQuota;
            MaxSize = effectiveMaxSize;
            _createQuotaExceededException = createQuotaExceededException;
            _buffer = bufferManager.TakeBuffer(initialSize);
            _capacity = Math.Min(_buffer.Length, effectiveMaxSize);
        }

        /// <summary>The hard ceiling on <see cref="WrittenCount"/>; the <c>effectiveMaxSize</c> constructor argument.</summary>
        internal int MaxSize { get; }

        /// <summary>The quota reported in the exception message; the <c>maxSizeQuota</c> constructor argument.</summary>
        internal int MaxSizeQuota => _maxSizeQuota;

        /// <summary>
        /// Bytes usable without growing: <c>Math.Min(rentedArray.Length, MaxSize)</c>. The pool routinely
        /// over-delivers (it buckets by size), and the surplus is used rather than wasted.
        /// </summary>
        internal int Capacity
        {
            get
            {
                ThrowIfUnavailable();
                return _capacity;
            }
        }

        /// <summary>Bytes committed so far through <see cref="Advance"/>.</summary>
        internal int WrittenCount
        {
            get
            {
                ThrowIfUnavailable();
                return _writtenCount;
            }
        }

        /// <summary>Bytes writable before a grow is needed.</summary>
        internal int FreeCapacity
        {
            get
            {
                ThrowIfUnavailable();
                return _capacity - _writtenCount;
            }
        }

        /// <summary>
        /// Returns the free region, growing first if it holds fewer than <paramref name="sizeHint"/> bytes.
        /// </summary>
        /// <remarks>
        /// The span is clamped to the remaining quota. That matters: an <see cref="IBufferWriter{T}"/> consumer
        /// writes straight into the span, so an unclamped span would let it place bytes beyond
        /// <see cref="MaxSize"/> before anything checked. When the quota leaves no room this throws the quota
        /// exception rather than returning an empty span &#8212; an empty span breaks the
        /// <see cref="IBufferWriter{T}"/> contract and can spin a consumer forever.
        /// </remarks>
        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacityFor(sizeHint);
            return new Span<byte>(_buffer, _writtenCount, _capacity - _writtenCount);
        }

        /// <inheritdoc cref="GetSpan"/>
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacityFor(sizeHint);
            return new Memory<byte>(_buffer, _writtenCount, _capacity - _writtenCount);
        }

        /// <summary>Commits <paramref name="count"/> bytes of the region last returned by
        /// <see cref="GetSpan"/>/<see cref="GetMemory"/>.</summary>
        public void Advance(int count)
        {
            ThrowIfUnavailable();

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Value must be non-negative.");
            }

            if (count > _capacity - _writtenCount)
            {
                throw new InvalidOperationException("Cannot advance past the end of the buffer.");
            }

            _writtenCount += count;
        }

        /// <summary>
        /// Rents a larger buffer, copies the written bytes across and returns the old one to the pool.
        /// </summary>
        /// <returns><see langword="false"/> when <see cref="Capacity"/> already equals <see cref="MaxSize"/>,
        /// in which case nothing was rented and nothing changed.</returns>
        internal bool TryGrow()
        {
            ThrowIfUnavailable();

            if (_capacity >= MaxSize)
            {
                return false;
            }

            // Doubling, matching the growth the hand-written buffered-read loops used. The long arithmetic
            // only differs from int arithmetic where the int form would have overflowed to a negative size
            // and thrown out of TakeBuffer.
            int requestedSize = (int)Math.Min((long)_capacity * 2, MaxSize);
            if (requestedSize <= _capacity)
            {
                // _capacity is 0 because the pool handed back a zero-length array; doubling cannot make progress.
                requestedSize = MaxSize;
            }

            byte[] newBuffer = _bufferManager.TakeBuffer(requestedSize);
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _writtenCount);
            _bufferManager.ReturnBuffer(_buffer);
            _buffer = newBuffer;
            _capacity = Math.Min(newBuffer.Length, MaxSize);
            return true;
        }

        /// <summary>Builds the quota exception without throwing it, for callers that enforce the ceiling themselves.</summary>
        internal Exception CreateQuotaExceededException() => _createQuotaExceededException(_maxSizeQuota);

        /// <summary>
        /// Hands the pooled array to the caller, who from then on owes
        /// <see cref="BufferManager.ReturnBuffer"/> to the same <see cref="BufferManager"/>.
        /// </summary>
        /// <remarks>
        /// The array is returned raw, at its full <c>Length</c> &#8212; never trimmed or copied to
        /// <paramref name="writtenCount"/> bytes. Two reasons. <c>InternalBufferManager.ReturnBuffer</c> throws
        /// <see cref="ArgumentException"/> when the array length does not match the pool's bucket size, so a
        /// trimmed array blows up later at <c>Message.Close()</c> time. And callers such as
        /// <c>BinaryMessageEncoderFactory.AddSessionInformationToMessage</c> deliberately use the slack past
        /// <paramref name="writtenCount"/>.
        /// </remarks>
        internal byte[] DetachBuffer(out int writtenCount)
        {
            ThrowIfUnavailable();

            byte[] buffer = _buffer;
            writtenCount = _writtenCount;
            _buffer = null;
            _detached = true;
            return buffer;
        }

        /// <summary>
        /// Returns the pooled array unless <see cref="DetachBuffer"/> already gave it away.
        /// </summary>
        /// <remarks>
        /// Idempotent, and it raises nothing of its own - not even when the writer has already been disposed or
        /// detached. That matters because it runs on exception unwind, where a throw would replace the
        /// <c>QuotaExceededException</c> that callers such as <c>ProtocolException</c> handling and
        /// <c>TransportDuplexSessionChannel</c> pattern-match on. (A <see cref="BufferManager"/> that throws
        /// out of <see cref="BufferManager.ReturnBuffer"/> still propagates; the array handed back is always the
        /// unmodified one that was rented, which is the condition the pooled implementation checks.)
        /// </remarks>
        public void Dispose()
        {
            byte[] buffer = _buffer;
            if (buffer != null)
            {
                _buffer = null;
                _bufferManager.ReturnBuffer(buffer);
            }
        }

        private void EnsureCapacityFor(int sizeHint)
        {
            ThrowIfUnavailable();

            if (sizeHint < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeHint), sizeHint, "Value must be non-negative.");
            }

            // IBufferWriter<T> requires a non-empty buffer, so a hint of 0 means "at least one byte".
            int required = sizeHint == 0 ? 1 : sizeHint;

            while (_capacity - _writtenCount < required)
            {
                if (!TryGrow())
                {
                    throw CreateQuotaExceededException();
                }
            }
        }

        private void ThrowIfUnavailable()
        {
            if (_buffer == null)
            {
                throw new ObjectDisposedException(nameof(BufferManagerBufferWriter),
                    _detached
                        ? "The buffer was detached; this writer cannot be used again."
                        : "The buffer was returned to the BufferManager; this writer cannot be used again.");
            }
        }
    }
}
