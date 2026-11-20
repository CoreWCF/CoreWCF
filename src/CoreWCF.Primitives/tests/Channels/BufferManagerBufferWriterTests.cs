// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using Xunit;

// Not namespaced ...Tests.Channels on purpose: that would shadow CoreWCF.Channels for every other file in
// CoreWCF.Primitives.Tests that resolves it as a relative "Channels.".
namespace CoreWCF.Primitives.Tests
{
    /// <summary>
    /// Covers <see cref="BufferManagerBufferWriter"/> and <see cref="BufferedMessageStreamHelper"/>, both of
    /// which are compiled into this assembly from <c>$(CommonPath)</c> by the test csproj.
    /// </summary>
    /// <remarks>
    /// The highest value tests here are <see cref="BufferMessageStreamAsync_MatchesLegacyLoop"/> and friends:
    /// they run the loop that <c>HttpInput.BufferMessageStreamAsync</c> used before this refactor, verbatim,
    /// against the new helper and compare payloads, exceptions and pool traffic.
    /// </remarks>
    public class BufferManagerBufferWriterTests
    {
        private const int InitialBufferSize = 8192; // ConnectionOrientedTransportDefaults.ConnectionBufferSize

        private static readonly Func<int, Exception> s_quotaFactory =
            quota => new InvalidOperationException("quota exceeded: " + quota.ToString());

        #region Construction and basic state

        [Fact]
        public void Constructor_RentsFromTheSuppliedManager()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 65536))
            {
                Assert.Equal(new[] { InitialBufferSize }, manager.Requests);
                Assert.Equal(1, manager.TakeCount);
                Assert.Equal(InitialBufferSize, writer.Capacity);
                Assert.Equal(0, writer.WrittenCount);
                Assert.Equal(InitialBufferSize, writer.FreeCapacity);
                Assert.Equal(65536, writer.MaxSize);
                Assert.Equal(65536, writer.MaxSizeQuota);
            }
        }

        [Fact]
        public void Constructor_UsesTheSurplusWhenThePoolOverDelivers()
        {
            RecordingBufferManager manager = new RecordingBufferManager(padding: 512);

            using (BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 65536))
            {
                // The pool buckets by size, so it routinely hands back more than was asked for. Use it.
                Assert.Equal(InitialBufferSize + 512, writer.Capacity);
            }
        }

        [Fact]
        public void Constructor_ClampsCapacityToMaxSize()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 100))
            {
                Assert.Equal(100, writer.Capacity);
                Assert.Equal(100, writer.FreeCapacity);
            }
        }

        [Fact]
        public void Constructor_RejectsInvalidArguments()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            Assert.Throws<ArgumentNullException>(() => { new BufferManagerBufferWriter(null, 1, 1, 1, s_quotaFactory).Dispose(); });
            Assert.Throws<ArgumentNullException>(() => { new BufferManagerBufferWriter(manager, 1, 1, 1, null).Dispose(); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new BufferManagerBufferWriter(manager, -1, 1, 1, s_quotaFactory).Dispose(); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new BufferManagerBufferWriter(manager, 1, -1, 1, s_quotaFactory).Dispose(); });
            Assert.Throws<ArgumentOutOfRangeException>(() => { new BufferManagerBufferWriter(manager, 1, 1, -1, s_quotaFactory).Dispose(); });
            Assert.Equal(0, manager.TakeCount);
        }

        #endregion

        #region IBufferWriter behaviour

        [Fact]
        public void GetSpanAndAdvance_WriteThroughTheInterface()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 65536))
            {
                IBufferWriter<byte> bufferWriter = writer;
                Span<byte> span = bufferWriter.GetSpan(4);
                span[0] = 1;
                span[1] = 2;
                span[2] = 3;
                span[3] = 4;
                bufferWriter.Advance(4);

                Assert.Equal(4, writer.WrittenCount);
                Assert.Equal(writer.Capacity - 4, writer.FreeCapacity);

                byte[] buffer = writer.DetachBuffer(out int writtenCount);
                Assert.Equal(4, writtenCount);
                Assert.Equal(new byte[] { 1, 2, 3, 4 }, buffer.Take(4).ToArray());
                manager.ReturnBuffer(buffer);
            }
        }

        [Fact]
        public void GetSpan_ReturnsTheWholeFreeRegion()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 65536))
            {
                Assert.Equal(InitialBufferSize, writer.GetSpan().Length);
                Assert.Equal(InitialBufferSize, writer.GetMemory().Length);
                writer.Advance(100);
                Assert.Equal(InitialBufferSize - 100, writer.GetSpan().Length);
                Assert.Equal(InitialBufferSize - 100, writer.GetMemory(1).Length);
            }
        }

        [Fact]
        public void GetSpan_GrowsWhenTheHintDoesNotFit()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, 16, 65536))
            {
                writer.Advance(16);
                Assert.Equal(0, writer.FreeCapacity);

                Span<byte> span = writer.GetSpan(100);
                Assert.True(span.Length >= 100);
                // 16 -> 32 -> 64 -> 128: doubling, one rent per step.
                Assert.Equal(new[] { 16, 32, 64, 128 }, manager.Requests);
            }
        }

        [Fact]
        public void GetSpan_ThrowsTheQuotaExceptionRatherThanReturningAnEmptySpan()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, 16, maxSize: 16))
            {
                writer.Advance(16);
                Assert.Equal(0, writer.FreeCapacity);

                // An empty span would violate the IBufferWriter<T> contract and can spin a consumer forever.
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => { writer.GetSpan(); });
                Assert.Equal("quota exceeded: 16", exception.Message);
                Assert.Throws<InvalidOperationException>(() => { writer.GetMemory(); });
            }
        }

        [Fact]
        public void GetSpan_RejectsNegativeHints()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 65536))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => { writer.GetSpan(-1); });
                Assert.Throws<ArgumentOutOfRangeException>(() => { writer.GetMemory(-1); });
            }
        }

        [Fact]
        public void Advance_RejectsNegativeAndOverlongCounts()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 65536))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => { writer.Advance(-1); });
                Assert.Throws<InvalidOperationException>(() => { writer.Advance(writer.Capacity + 1); });
                Assert.Equal(0, writer.WrittenCount);
            }
        }

        [Fact]
        public void MaxSizeQuota_IsReportedWhileEffectiveMaxSizeIsEnforced()
        {
            // BufferedMessageWriter enforces maxSizeQuota + initialOffset but reports maxSizeQuota, so the two
            // values genuinely differ and must not be collapsed into one.
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer =
                new BufferManagerBufferWriter(manager, 16, maxSizeQuota: 100, effectiveMaxSize: 150, createQuotaExceededException: s_quotaFactory))
            {
                writer.GetSpan(150);
                Assert.Equal(150, writer.Capacity);
                writer.Advance(150);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => { writer.GetSpan(); });
                Assert.Equal("quota exceeded: 100", exception.Message);
            }
        }

        #endregion

        #region Growth and pool traffic

        [Fact]
        public void TryGrow_DoublesAndReturnsTheSupersededBuffer()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, 16, 65536))
            {
                writer.GetSpan(4)[0] = 42;
                writer.Advance(4);
                byte[] first = manager.Taken[0];

                Assert.True(writer.TryGrow());

                Assert.Equal(new[] { 16, 32 }, manager.Requests);
                Assert.Same(first, Assert.Single(manager.Returned));
                Assert.Equal(1, manager.Outstanding);
                Assert.Equal(32, writer.Capacity);
                Assert.Equal(4, writer.WrittenCount);
                // The bytes written before the grow survived it.
                Assert.Equal(42, manager.Taken[1][0]);
            }
        }

        [Fact]
        public void TryGrow_ReturnsFalseAtMaxSizeWithoutRenting()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, 64, maxSize: 64))
            {
                Assert.False(writer.TryGrow());
                Assert.Equal(1, manager.TakeCount);
                Assert.Equal(0, manager.ReturnCount);
            }
        }

        [Fact]
        public void TryGrow_MakesProgressWhenThePoolHandsBackAZeroLengthArray()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, 0, maxSize: 64))
            {
                Assert.Equal(0, writer.Capacity);
                Assert.True(writer.TryGrow());
                Assert.Equal(64, writer.Capacity);
            }
        }

        [Fact]
        public void TryGrow_NeverExceedsMaxSize()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            using (BufferManagerBufferWriter writer = CreateWriter(manager, 16, maxSize: 40))
            {
                Assert.True(writer.TryGrow());  // 16 -> 32
                Assert.True(writer.TryGrow());  // 32 -> 40, clamped
                Assert.Equal(40, writer.Capacity);
                Assert.False(writer.TryGrow());
                Assert.Equal(new[] { 16, 32, 40 }, manager.Requests);
            }
        }

        #endregion

        #region Ownership

        [Fact]
        public void Dispose_ReturnsTheBufferExactlyOnce()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 65536);
            writer.Dispose();
            writer.Dispose();

            Assert.Equal(1, manager.ReturnCount);
            Assert.Equal(0, manager.Outstanding);
        }

        [Fact]
        public void DetachBuffer_TransfersOwnershipSoDisposeDoesNothing()
        {
            RecordingBufferManager manager = new RecordingBufferManager();
            byte[] detached;

            using (BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 65536))
            {
                writer.Advance(10);
                detached = writer.DetachBuffer(out int writtenCount);
                Assert.Equal(10, writtenCount);
            }

            Assert.Equal(0, manager.ReturnCount);
            Assert.Equal(1, manager.Outstanding);
            Assert.Same(manager.Taken[0], detached);

            // The caller owes the return, and the manager accepts it.
            manager.ReturnBuffer(detached);
            Assert.Equal(0, manager.Outstanding);
        }

        [Fact]
        public void DetachBuffer_HandsBackTheRawPooledArrayAtItsFullLength()
        {
            // InternalBufferManager.ReturnBuffer throws when the array length does not match its bucket size,
            // so the array must never be trimmed or copied down to WrittenCount.
            RecordingBufferManager manager = new RecordingBufferManager(padding: 512);

            using (BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 65536))
            {
                writer.Advance(10);
                byte[] detached = writer.DetachBuffer(out int writtenCount);

                Assert.Equal(InitialBufferSize + 512, detached.Length);
                Assert.Equal(10, writtenCount);
                manager.ReturnBuffer(detached);
            }
        }

        [Fact]
        public void UseAfterDetach_Throws()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 65536);
            byte[] detached = writer.DetachBuffer(out int _);

            Assert.Throws<ObjectDisposedException>(() => { writer.DetachBuffer(out int _); });
            Assert.Throws<ObjectDisposedException>(() => { writer.GetSpan(); });
            Assert.Throws<ObjectDisposedException>(() => { writer.GetMemory(); });
            Assert.Throws<ObjectDisposedException>(() => { writer.Advance(0); });
            Assert.Throws<ObjectDisposedException>(() => { writer.TryGrow(); });
            Assert.Throws<ObjectDisposedException>(() => { _ = writer.Capacity; });
            Assert.Throws<ObjectDisposedException>(() => { _ = writer.WrittenCount; });
            Assert.Throws<ObjectDisposedException>(() => { _ = writer.FreeCapacity; });

            writer.Dispose();
            Assert.Equal(0, manager.ReturnCount);
            manager.ReturnBuffer(detached);
        }

        [Fact]
        public void UseAfterDispose_Throws()
        {
            RecordingBufferManager manager = new RecordingBufferManager();

            BufferManagerBufferWriter writer = CreateWriter(manager, InitialBufferSize, 65536);
            writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => { writer.GetSpan(); });
            Assert.Throws<ObjectDisposedException>(() => { writer.DetachBuffer(out int _); });
            Assert.Throws<ObjectDisposedException>(() => { writer.TryGrow(); });
        }

        #endregion

        #region BufferedMessageStreamHelper: pool accounting

        [Fact]
        public async Task BufferMessageStreamAsync_BodyThatFits_RentsOnce()
        {
            RecordingBufferManager manager = new RecordingBufferManager();
            ChunkedStream stream = new ChunkedStream(CreateBody(100), int.MaxValue);

            ArraySegment<byte> result = await BufferedMessageStreamHelper.BufferMessageStreamAsync(
                stream, manager, maxBufferSize: 65536, initialBufferSize: InitialBufferSize,
                createQuotaExceededException: s_quotaFactory);

            Assert.Equal(100, result.Count);
            Assert.Equal(1, manager.TakeCount);
            Assert.Equal((long)InitialBufferSize, manager.TotalBytesRented);
            Assert.Equal((long)InitialBufferSize, manager.PeakOutstandingBytes);

            // Ownership passed to the caller with the segment: exactly one buffer is still out, and it is
            // the one the segment points at.
            Assert.Equal(0, manager.ReturnCount);
            Assert.Equal(1, manager.Outstanding);
            Assert.Same(manager.Taken[0], result.Array);
        }

        [Fact]
        public async Task BufferMessageStreamAsync_TwoGrowths_HoldsAtMostTwoBuffersAtOnce()
        {
            RecordingBufferManager manager = new RecordingBufferManager();
            ChunkedStream stream = new ChunkedStream(CreateBody(20000), int.MaxValue);

            ArraySegment<byte> result = await BufferedMessageStreamHelper.BufferMessageStreamAsync(
                stream, manager, maxBufferSize: 1 << 20, initialBufferSize: InitialBufferSize,
                createQuotaExceededException: s_quotaFactory);

            Assert.Equal(20000, result.Count);
            Assert.Equal(3, manager.TakeCount);
            Assert.Equal(new[] { 8192, 16384, 32768 }, manager.Requests);
            Assert.Equal(8192L + 16384L + 32768L, manager.TotalBytesRented);

            // The superseded buffer is returned right after the copy, so old and new coexist only across the
            // BlockCopy - never three at once. This is what pins the growth policy: if chunk-list growth were
            // ever reintroduced, or a superseded buffer left unreturned, this number changes.
            Assert.Equal(16384L + 32768L, manager.PeakOutstandingBytes);
            Assert.Equal(1, manager.Outstanding);
        }

        [Fact]
        public async Task BufferMessageStreamAsync_QuotaExceeded_ReturnsEveryBufferItRented()
        {
            RecordingBufferManager manager = new RecordingBufferManager();
            ChunkedStream stream = new ChunkedStream(CreateBody(8192), int.MaxValue);

            InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => BufferedMessageStreamHelper.BufferMessageStreamAsync(stream, manager, maxBufferSize: 4096,
                    initialBufferSize: InitialBufferSize, createQuotaExceededException: s_quotaFactory));

            Assert.Equal("quota exceeded: 4096", exception.Message);

            // The loop this replaces had no try, so it leaked the rented buffer on this path.
            Assert.Equal(manager.TakeCount, manager.ReturnCount);
            Assert.Equal(0, manager.Outstanding);
        }

        [Fact]
        public async Task BufferMessageStreamAsync_ReadFailure_ReturnsEveryBufferItRented()
        {
            RecordingBufferManager manager = new RecordingBufferManager();
            ChunkedStream stream = new ChunkedStream(CreateBody(100), int.MaxValue) { FailAfterBytes = 50 };

            await Assert.ThrowsAsync<IOException>(
                () => BufferedMessageStreamHelper.BufferMessageStreamAsync(stream, manager, maxBufferSize: 65536,
                    initialBufferSize: InitialBufferSize, createQuotaExceededException: s_quotaFactory));

            Assert.Equal(manager.TakeCount, manager.ReturnCount);
            Assert.Equal(0, manager.Outstanding);
        }

        [Fact]
        public async Task LegacyLoop_LeakedOnTheQuotaPath()
        {
            // Documents the bug the using block in BufferedMessageStreamHelper fixes. If this ever starts
            // failing the legacy oracle has drifted and the parity tests below are no longer meaningful.
            RecordingBufferManager manager = new RecordingBufferManager();
            ChunkedStream stream = new ChunkedStream(CreateBody(8192), int.MaxValue);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => LegacyBufferMessageStreamAsync(stream, manager, 4096, InitialBufferSize));

            Assert.Equal(1, manager.TakeCount);
            Assert.Equal(0, manager.ReturnCount);
            Assert.Equal(1, manager.Outstanding);
        }

        #endregion

        #region BufferedMessageStreamHelper: parity with the loop it replaces

        public static IEnumerable<object[]> ParityCases()
        {
            int[] maxBufferSizes = { 0, 100, 4096, 8192, 65536, 1000000 };
            int[] bodySizes = { 0, 1, 99, 100, 101, 4095, 4096, 4097, 8192, 8193, 20000, 65536, 65537 };
            int[] chunkSizes = { 1, 7, 4096, int.MaxValue };
            int[] paddings = { 0, 7 };

            foreach (int maxBufferSize in maxBufferSizes)
            {
                foreach (int bodySize in bodySizes)
                {
                    foreach (int chunkSize in chunkSizes)
                    {
                        // One byte at a time over a 64 KB body adds nothing the 7 byte case does not already
                        // cover, and it dominates the run time of this theory.
                        if (chunkSize == 1 && bodySize > 8193)
                        {
                            continue;
                        }

                        foreach (int padding in paddings)
                        {
                            yield return new object[] { maxBufferSize, bodySize, chunkSize, padding };
                        }
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(ParityCases))]
        public async Task BufferMessageStreamAsync_MatchesLegacyLoop(int maxBufferSize, int bodySize, int chunkSize, int padding)
        {
            byte[] body = CreateBody(bodySize);

            RecordingBufferManager legacyManager = new RecordingBufferManager(padding);
            ChunkedStream legacyStream = new ChunkedStream(body, chunkSize);
            (ArraySegment<byte> Segment, Exception Exception) legacy =
                await RunAsync(() => LegacyBufferMessageStreamAsync(legacyStream, legacyManager, maxBufferSize, InitialBufferSize));

            RecordingBufferManager manager = new RecordingBufferManager(padding);
            ChunkedStream stream = new ChunkedStream(body, chunkSize);
            (ArraySegment<byte> Segment, Exception Exception) actual =
                await RunAsync(() => BufferedMessageStreamHelper.BufferMessageStreamAsync(stream, manager,
                    maxBufferSize, InitialBufferSize, s_quotaFactory));

            if (legacy.Exception != null)
            {
                Assert.NotNull(actual.Exception);
                Assert.Equal(legacy.Exception.GetType(), actual.Exception.GetType());
                Assert.Equal(legacy.Exception.Message, actual.Exception.Message);
                Assert.Equal(legacy.Exception.InnerException?.GetType(), actual.Exception.InnerException?.GetType());
                return;
            }

            Assert.Null(actual.Exception);
            Assert.Equal(legacy.Segment.Count, actual.Segment.Count);
            Assert.Equal(Payload(legacy.Segment), Payload(actual.Segment));
            Assert.Equal(legacyStream.Disposed, stream.Disposed);

            if (padding == 0)
            {
                // With a pool that hands back exactly what was asked for, the rent sequence is identical too.
                Assert.Equal(legacyManager.Requests, manager.Requests);
                Assert.Equal(legacyManager.TotalBytesRented, manager.TotalBytesRented);
            }
        }

        [Theory]
        [InlineData(100)]
        [InlineData(4096)]
        [InlineData(8192)]
        public async Task BufferMessageStreamAsync_BodyOfExactlyMaxBufferSize_Throws(int maxBufferSize)
        {
            // The ceiling is checked as soon as the buffer fills, before another read is attempted, so a body
            // that is exactly maxBufferSize bytes long throws even though nothing follows it.
            RecordingBufferManager manager = new RecordingBufferManager();
            ChunkedStream stream = new ChunkedStream(CreateBody(maxBufferSize), int.MaxValue);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => BufferedMessageStreamHelper.BufferMessageStreamAsync(stream, manager, maxBufferSize,
                    InitialBufferSize, s_quotaFactory));
        }

        [Fact]
        public async Task BufferMessageStreamAsync_ZeroMaxBufferSize_ReadsNothingAndDoesNotThrow()
        {
            RecordingBufferManager manager = new RecordingBufferManager();
            ChunkedStream stream = new ChunkedStream(CreateBody(100), int.MaxValue);

            ArraySegment<byte> result = await BufferedMessageStreamHelper.BufferMessageStreamAsync(
                stream, manager, maxBufferSize: 0, initialBufferSize: InitialBufferSize,
                createQuotaExceededException: s_quotaFactory);

            Assert.Equal(0, result.Count);
            Assert.False(stream.Disposed);
            Assert.Equal(0, stream.Position);
            Assert.Equal(1, manager.Outstanding);
        }

        [Fact]
        public async Task BufferMessageStreamAsync_DisposesTheStreamOnEndOfStream()
        {
            RecordingBufferManager manager = new RecordingBufferManager();
            ChunkedStream stream = new ChunkedStream(CreateBody(10), int.MaxValue);

            await BufferedMessageStreamHelper.BufferMessageStreamAsync(stream, manager, 65536, InitialBufferSize, s_quotaFactory);

            Assert.True(stream.Disposed);
        }

        [Fact]
        public async Task BufferMessageStreamAsync_UsesTheSuppliedManagerAndNotASharedPool()
        {
            // The defect in the abandoned draft was rewiring this path onto ArrayPool<byte>.Shared, which
            // silently bypasses the transport's configured BufferManager and its MaxBufferPoolSize.
            RecordingBufferManager manager = new RecordingBufferManager();
            ChunkedStream stream = new ChunkedStream(CreateBody(20000), 1);

            ArraySegment<byte> result = await BufferedMessageStreamHelper.BufferMessageStreamAsync(
                stream, manager, 1 << 20, InitialBufferSize, s_quotaFactory);

            Assert.Same(manager.Taken[manager.Taken.Count - 1], result.Array);
            Assert.All(manager.Returned, buffer => Assert.Contains(buffer, manager.Taken));
        }

        #endregion

        #region Managed-heap allocation

        // GC.GetAllocatedBytesForCurrentThread is not part of the net472 surface, so this has to be compiled
        // out rather than merely skipped - compiling a net472-unavailable API into a multi-targeted test
        // project is what red-lit the earlier attempt at this refactor.
#if !NETFRAMEWORK
        [NetCoreOnlyFact]
        public void WriteOperations_DoNotAllocateOnTheManagedHeap()
        {
            // A smoke alarm, not a benchmark: it catches per-call garbage that pool accounting cannot see -
            // a lambda allocated per GetSpan, a boxed Memory<byte>, an ArraySegment promoted to the heap.
            // It deliberately measures the synchronous surface only; the async helper allocates one state
            // machine per call by design, which would make the test about the compiler rather than this code.
            const int Iterations = 10_000;
            PreallocatedBufferManager manager = new PreallocatedBufferManager(1 << 20);

            RunWriteLoop(manager, 1000); // warm up: JIT and let tiered compilation settle before measuring

            long before = GC.GetAllocatedBytesForCurrentThread();
            RunWriteLoop(manager, Iterations);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            // Divided by the iteration count so a stray one-off allocation rounds away instead of flaking the
            // build. If this ever proves noisy in CI, raise the ceiling - do not delete the test.
            Assert.Equal(0L, allocated / Iterations);
        }

        private static void RunWriteLoop(BufferManager manager, int iterations)
        {
            using (BufferManagerBufferWriter writer =
                new BufferManagerBufferWriter(manager, 1 << 20, 1 << 20, 1 << 20, s_quotaFactory))
            {
                for (int i = 0; i < iterations; i++)
                {
                    Span<byte> span = writer.GetSpan(16);
                    span[0] = (byte)i;
                    Memory<byte> memory = writer.GetMemory(16);
                    memory.Span[1] = (byte)i;
                    writer.Advance(2);
                    _ = writer.Capacity;
                    _ = writer.WrittenCount;
                    _ = writer.FreeCapacity;
                }
            }
        }
#endif

        #endregion

        #region Helpers

        private static BufferManagerBufferWriter CreateWriter(BufferManager manager, int initialSize, int maxSize) =>
            new BufferManagerBufferWriter(manager, initialSize, maxSize, maxSize, s_quotaFactory);

        private static byte[] CreateBody(int size)
        {
            byte[] body = new byte[size];
            for (int i = 0; i < size; i++)
            {
                body[i] = (byte)(i % 251);
            }

            return body;
        }

        private static byte[] Payload(ArraySegment<byte> segment)
        {
            byte[] payload = new byte[segment.Count];
            Buffer.BlockCopy(segment.Array, segment.Offset, payload, 0, segment.Count);
            return payload;
        }

        private static async Task<(ArraySegment<byte> Segment, Exception Exception)> RunAsync(
            Func<Task<ArraySegment<byte>>> operation)
        {
            try
            {
                return (await operation(), null);
            }
            catch (Exception exception)
            {
                return (default, exception);
            }
        }

        /// <summary>
        /// The body of <c>HttpInput.BufferMessageStreamAsync</c> as it stood before this refactor, copied
        /// verbatim apart from the injected initial size and exception factory. It is the oracle the parity
        /// tests compare against; do not "clean it up".
        /// </summary>
        private static async Task<ArraySegment<byte>> LegacyBufferMessageStreamAsync(Stream stream,
            BufferManager bufferManager, int maxBufferSize, int initialBufferSize)
        {
            byte[] buffer = bufferManager.TakeBuffer(initialBufferSize);
            int offset = 0;
            int currentBufferSize = Math.Min(buffer.Length, maxBufferSize);

            while (offset < currentBufferSize)
            {
                int count = await stream.ReadAsync(buffer, offset, currentBufferSize - offset);
                if (count == 0)
                {
                    stream.Dispose();
                    break;
                }

                offset += count;
                if (offset == currentBufferSize)
                {
                    if (currentBufferSize >= maxBufferSize)
                    {
                        throw s_quotaFactory(maxBufferSize);
                    }

                    currentBufferSize = Math.Min(currentBufferSize * 2, maxBufferSize);
                    byte[] temp = bufferManager.TakeBuffer(currentBufferSize);
                    Buffer.BlockCopy(buffer, 0, temp, 0, offset);
                    bufferManager.ReturnBuffer(buffer);
                    buffer = temp;
                }
            }

            return new ArraySegment<byte>(buffer, 0, offset);
        }

        /// <summary>
        /// A <see cref="BufferManager"/> that accounts for every rent and return, and fails loudly on a double
        /// return or on a return of an array it never issued.
        /// </summary>
        /// <remarks>
        /// <c>SimpleBufferManager</c> in tests/Helpers/MessageTestUtilities.cs records nothing, and
        /// <c>GCBufferManager</c> makes <c>ReturnBuffer</c> a no-op, so neither would notice any of the bugs
        /// these tests are here to catch. Hence the extra type.
        /// </remarks>
        private sealed class RecordingBufferManager : BufferManager
        {
            private readonly int _padding;
            private readonly List<byte[]> _outstanding = new List<byte[]>();

            internal RecordingBufferManager(int padding = 0)
            {
                _padding = padding;
            }

            /// <summary>Sizes asked for, in order.</summary>
            internal List<int> Requests { get; } = new List<int>();

            /// <summary>Arrays handed out, in order.</summary>
            internal List<byte[]> Taken { get; } = new List<byte[]>();

            /// <summary>Arrays handed back, in order.</summary>
            internal List<byte[]> Returned { get; } = new List<byte[]>();

            internal int TakeCount => Taken.Count;

            internal int ReturnCount => Returned.Count;

            /// <summary>Arrays rented but not yet given back.</summary>
            internal int Outstanding => _outstanding.Count;

            internal long TotalBytesRented { get; private set; }

            internal long OutstandingBytes { get; private set; }

            /// <summary>The high-water mark of <see cref="OutstandingBytes"/>; what pins the growth policy.</summary>
            internal long PeakOutstandingBytes { get; private set; }

            public override byte[] TakeBuffer(int bufferSize)
            {
                if (bufferSize < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(bufferSize));
                }

                byte[] buffer = new byte[bufferSize + _padding];
                Requests.Add(bufferSize);
                Taken.Add(buffer);
                _outstanding.Add(buffer);
                TotalBytesRented += buffer.Length;
                OutstandingBytes += buffer.Length;
                if (OutstandingBytes > PeakOutstandingBytes)
                {
                    PeakOutstandingBytes = OutstandingBytes;
                }

                return buffer;
            }

            public override void ReturnBuffer(byte[] buffer)
            {
                if (buffer == null)
                {
                    throw new ArgumentNullException(nameof(buffer));
                }

                if (!Taken.Any(taken => ReferenceEquals(taken, buffer)))
                {
                    throw new InvalidOperationException("Returned an array this BufferManager never issued.");
                }

                int index = _outstanding.FindIndex(outstanding => ReferenceEquals(outstanding, buffer));
                if (index < 0)
                {
                    throw new InvalidOperationException("Returned the same array twice.");
                }

                _outstanding.RemoveAt(index);
                Returned.Add(buffer);
                OutstandingBytes -= buffer.Length;
            }

            public override void Clear()
            {
            }
        }

        /// <summary>
        /// Hands out one array created up front, so a measured region can rent without allocating.
        /// </summary>
        private sealed class PreallocatedBufferManager : BufferManager
        {
            private readonly byte[] _buffer;

            internal PreallocatedBufferManager(int size)
            {
                _buffer = new byte[size];
            }

            public override byte[] TakeBuffer(int bufferSize) => _buffer;

            public override void ReturnBuffer(byte[] buffer)
            {
            }

            public override void Clear()
            {
            }
        }

        /// <summary>A body stream that hands out at most <c>maxChunk</c> bytes per read, and can be made to fail.</summary>
        private sealed class ChunkedStream : Stream
        {
            private readonly byte[] _data;
            private readonly int _maxChunk;
            private int _position;

            internal ChunkedStream(byte[] data, int maxChunk)
            {
                _data = data;
                _maxChunk = maxChunk;
            }

            internal bool Disposed { get; private set; }

            /// <summary>When set, the read that would take the position past this many bytes throws instead.</summary>
            internal int FailAfterBytes { get; set; } = -1;

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => _data.Length;

            public override long Position
            {
                get => _position;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (FailAfterBytes >= 0 && _position >= FailAfterBytes)
                {
                    throw new IOException("Simulated read failure.");
                }

                int available = _data.Length - _position;
                int toRead = Math.Min(Math.Min(count, available), _maxChunk);
                if (FailAfterBytes >= 0)
                {
                    toRead = Math.Min(toRead, FailAfterBytes - _position);
                }

                Buffer.BlockCopy(_data, _position, buffer, offset, toRead);
                _position += toRead;
                return toRead;
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                Task.FromResult(Read(buffer, offset, count));

            protected override void Dispose(bool disposing)
            {
                Disposed = true;
                base.Dispose(disposing);
            }

            public override void Flush() => throw new NotSupportedException();

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        #endregion
    }
}
