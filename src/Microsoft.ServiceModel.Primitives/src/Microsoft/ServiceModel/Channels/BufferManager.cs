using System;
using Microsoft.Runtime;

namespace Microsoft.ServiceModel.Channels
{
    public abstract class BufferManager
    {
        public abstract void ReturnBuffer(byte[] buffer);
        public abstract byte[] TakeBuffer(int bufferSize);
        public abstract void Clear();

        public static BufferManager CreateBufferManager(long maxBufferPoolSize, int maxBufferSize)
        {
            if (maxBufferPoolSize < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maxBufferPoolSize),
                    maxBufferPoolSize, SR.ValueMustBeNonNegative));
            }

            if (maxBufferSize < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maxBufferSize),
                    maxBufferSize, SR.ValueMustBeNonNegative));
            }

            return new WrappingBufferManager(InternalBufferManager.Create(maxBufferPoolSize, maxBufferSize));
        }

        internal static InternalBufferManager GetInternalBufferManager(BufferManager bufferManager)
        {
            if (bufferManager is WrappingBufferManager)
            {
                return ((WrappingBufferManager)bufferManager).InternalBufferManager;
            }
            else
            {
                return new WrappingInternalBufferManager(bufferManager);
            }
        }

        class WrappingBufferManager : BufferManager
        {
            InternalBufferManager innerBufferManager;

            public WrappingBufferManager(InternalBufferManager innerBufferManager)
            {
                this.innerBufferManager = innerBufferManager;
            }

            public InternalBufferManager InternalBufferManager
            {
                get { return innerBufferManager; }
            }

            public override byte[] TakeBuffer(int bufferSize)
            {
                if (bufferSize < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize,
                        SR.ValueMustBeNonNegative));
                }

                return innerBufferManager.TakeBuffer(bufferSize);
            }

            public override void ReturnBuffer(byte[] buffer)
            {
                if (buffer == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("buffer");
                }

                innerBufferManager.ReturnBuffer(buffer);
            }

            public override void Clear()
            {
                innerBufferManager.Clear();
            }
        }

        class WrappingInternalBufferManager : InternalBufferManager
        {
            BufferManager innerBufferManager;

            public WrappingInternalBufferManager(BufferManager innerBufferManager)
            {
                this.innerBufferManager = innerBufferManager;
            }

            public override void Clear()
            {
                innerBufferManager.Clear();
            }

            public override void ReturnBuffer(byte[] buffer)
            {
                innerBufferManager.ReturnBuffer(buffer);
            }

            public override byte[] TakeBuffer(int bufferSize)
            {
                return innerBufferManager.TakeBuffer(bufferSize);
            }
        }
    }
}