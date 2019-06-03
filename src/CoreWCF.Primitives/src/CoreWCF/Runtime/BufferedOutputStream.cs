using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Runtime
{
    internal class BufferedOutputStream : Stream
    {
        InternalBufferManager bufferManager;

        byte[][] chunks;

        int chunkCount;
        byte[] currentChunk;
        int currentChunkSize;
        int maxSize;
        int maxSizeQuota;
        int totalSize;
        bool callerReturnsBuffer;
        bool bufferReturned;
        bool initialized;

        // requires an explicit call to Init() by the caller
        public BufferedOutputStream()
        {
            chunks = new byte[4][];
        }

        public BufferedOutputStream(int initialSize, int maxSize, InternalBufferManager bufferManager)
            : this()
        {
            Reinitialize(initialSize, maxSize, bufferManager);
        }

        public BufferedOutputStream(int maxSize)
            : this(0, maxSize, InternalBufferManager.Create(0, int.MaxValue))
        {
        }

        public override bool CanRead
        {
            get
            {
                return false;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return true;
            }
        }

        public override long Length
        {
            get
            {
                return totalSize;
            }
        }

        public override long Position
        {
            get
            {
                throw Fx.Exception.AsError(new NotSupportedException(SR.SeekNotSupported));
            }
            set
            {
                throw Fx.Exception.AsError(new NotSupportedException(SR.SeekNotSupported));
            }
        }

        public void Reinitialize(int initialSize, int maxSizeQuota, InternalBufferManager bufferManager)
        {
            Reinitialize(initialSize, maxSizeQuota, maxSizeQuota, bufferManager);
        }

        public void Reinitialize(int initialSize, int maxSizeQuota, int effectiveMaxSize, InternalBufferManager bufferManager)
        {
            Fx.Assert(!initialized, "Clear must be called before re-initializing stream");
            this.maxSizeQuota = maxSizeQuota;
            maxSize = effectiveMaxSize;
            this.bufferManager = bufferManager;
            currentChunk = bufferManager.TakeBuffer(initialSize);
            currentChunkSize = 0;
            totalSize = 0;
            chunkCount = 1;
            chunks[0] = currentChunk;
            initialized = true;
        }

        void AllocNextChunk(int minimumChunkSize)
        {
            int newChunkSize;
            if (currentChunk.Length > (int.MaxValue / 2))
            {
                newChunkSize = int.MaxValue;
            }
            else
            {
                newChunkSize = currentChunk.Length * 2;
            }
            if (minimumChunkSize > newChunkSize)
            {
                newChunkSize = minimumChunkSize;
            }
            byte[] newChunk = bufferManager.TakeBuffer(newChunkSize);
            if (chunkCount == chunks.Length)
            {
                byte[][] newChunks = new byte[chunks.Length * 2][];
                Array.Copy(chunks, newChunks, chunks.Length);
                chunks = newChunks;
            }
            chunks[chunkCount++] = newChunk;
            currentChunk = newChunk;
            currentChunkSize = 0;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.FromException<int>(Fx.Exception.AsError(new NotSupportedException(SR.ReadNotSupported)));
        }

        //public override IAsyncResult BeginRead(byte[] buffer, int offset, int size, AsyncCallback callback, object state)
        //{
        //    throw Fx.Exception.AsError(new NotSupportedException(SR.ReadNotSupported));
        //}

        //public override int EndRead(IAsyncResult result)
        //{
        //    throw Fx.Exception.AsError(new NotSupportedException(SR.ReadNotSupported));
        //}

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        //public override IAsyncResult BeginWrite(byte[] buffer, int offset, int size, AsyncCallback callback, object state)
        //{
        //    Write(buffer, offset, size);
        //    return new CompletedAsyncResult(callback, state);
        //}

        //public override void EndWrite(IAsyncResult result)
        //{
        //    CompletedAsyncResult.End(result);
        //}

        public void Clear()
        {
            if (!callerReturnsBuffer)
            {
                for (int i = 0; i < chunkCount; i++)
                {
                    bufferManager.ReturnBuffer(chunks[i]);
                    chunks[i] = null;
                }
            }

            callerReturnsBuffer = false;
            initialized = false;
            bufferReturned = false;
            chunkCount = 0;
            currentChunk = null;
        }

        //public override void Close()
        //{
        //}

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int size)
        {
            throw Fx.Exception.AsError(new NotSupportedException(SR.ReadNotSupported));
        }

        public override int ReadByte()
        {
            throw Fx.Exception.AsError(new NotSupportedException(SR.ReadNotSupported));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw Fx.Exception.AsError(new NotSupportedException(SR.SeekNotSupported));
        }

        public override void SetLength(long value)
        {
            throw Fx.Exception.AsError(new NotSupportedException(SR.SeekNotSupported));
        }

        public MemoryStream ToMemoryStream()
        {
            int bufferSize;
            byte[] buffer = ToArray(out bufferSize);
            return new MemoryStream(buffer, 0, bufferSize);
        }

        public byte[] ToArray(out int bufferSize)
        {
            Fx.Assert(initialized, "No data to return from uninitialized stream");
            Fx.Assert(!bufferReturned, "ToArray cannot be called more than once");

            byte[] buffer;
            if (chunkCount == 1)
            {
                buffer = currentChunk;
                bufferSize = currentChunkSize;
                callerReturnsBuffer = true;
            }
            else
            {
                buffer = bufferManager.TakeBuffer(totalSize);
                int offset = 0;
                int count = chunkCount - 1;
                for (int i = 0; i < count; i++)
                {
                    byte[] chunk = chunks[i];
                    Buffer.BlockCopy(chunk, 0, buffer, offset, chunk.Length);
                    offset += chunk.Length;
                }
                Buffer.BlockCopy(currentChunk, 0, buffer, offset, currentChunkSize);
                bufferSize = totalSize;
            }

            bufferReturned = true;
            return buffer;
        }

        public void Skip(int size)
        {
            WriteCore(null, 0, size);
        }

        public override void Write(byte[] buffer, int offset, int size)
        {
            WriteCore(buffer, offset, size);
        }

        protected virtual Exception CreateQuotaExceededException(int maxSizeQuota)
        {
            return new InvalidOperationException(SR.Format(SR.BufferedOutputStreamQuotaExceeded, maxSizeQuota));
        }

        void WriteCore(byte[] buffer, int offset, int size)
        {
            Fx.Assert(initialized, "Cannot write to uninitialized stream");
            Fx.Assert(!bufferReturned, "Cannot write to stream once ToArray has been called.");

            if (size < 0)
            {
                throw Fx.Exception.ArgumentOutOfRange("size", size, SR.ValueMustBeNonNegative);
            }

            if ((int.MaxValue - size) < totalSize)
            {
                throw Fx.Exception.AsError(CreateQuotaExceededException(maxSizeQuota));
            }

            int newTotalSize = totalSize + size;
            if (newTotalSize > maxSize)
            {
                throw Fx.Exception.AsError(CreateQuotaExceededException(maxSizeQuota));
            }

            int remainingSizeInChunk = currentChunk.Length - currentChunkSize;
            if (size > remainingSizeInChunk)
            {
                if (remainingSizeInChunk > 0)
                {
                    if (buffer != null)
                    {
                        Buffer.BlockCopy(buffer, offset, currentChunk, currentChunkSize, remainingSizeInChunk);
                    }
                    currentChunkSize = currentChunk.Length;
                    offset += remainingSizeInChunk;
                    size -= remainingSizeInChunk;
                }
                AllocNextChunk(size);
            }

            if (buffer != null)
            {
                Buffer.BlockCopy(buffer, offset, currentChunk, currentChunkSize, size);
            }
            totalSize = newTotalSize;
            currentChunkSize += size;
        }

        public override void WriteByte(byte value)
        {
            Fx.Assert(initialized, "Cannot write to uninitialized stream");
            Fx.Assert(!bufferReturned, "Cannot write to stream once ToArray has been called.");

            if (totalSize == maxSize)
            {
                throw Fx.Exception.AsError(CreateQuotaExceededException(maxSize));
            }
            if (currentChunkSize == currentChunk.Length)
            {
                AllocNextChunk(1);
            }
            currentChunk[currentChunkSize++] = value;
        }
    }

}