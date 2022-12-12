// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
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
                    maxBufferPoolSize, SRCommon.ValueMustBeNonNegative));
            }

            if (maxBufferSize < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maxBufferSize),
                    maxBufferSize, SRCommon.ValueMustBeNonNegative));
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

        private class WrappingBufferManager : BufferManager
        {
            public WrappingBufferManager(InternalBufferManager innerBufferManager)
            {
                InternalBufferManager = innerBufferManager;
            }

            public InternalBufferManager InternalBufferManager { get; }

            public override byte[] TakeBuffer(int bufferSize)
            {
                if (bufferSize < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize,
                        SRCommon.ValueMustBeNonNegative));
                }

                return InternalBufferManager.TakeBuffer(bufferSize);
            }

            public override void ReturnBuffer(byte[] buffer)
            {
                if (buffer == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(buffer));
                }

                InternalBufferManager.ReturnBuffer(buffer);
            }

            public override void Clear()
            {
                InternalBufferManager.Clear();
            }
        }

        private class WrappingInternalBufferManager : InternalBufferManager
        {
            private readonly BufferManager _innerBufferManager;

            public WrappingInternalBufferManager(BufferManager innerBufferManager)
            {
                _innerBufferManager = innerBufferManager;
            }

            public override void Clear()
            {
                _innerBufferManager.Clear();
            }

            public override void ReturnBuffer(byte[] buffer)
            {
                _innerBufferManager.ReturnBuffer(buffer);
            }

            public override byte[] TakeBuffer(int bufferSize)
            {
                return _innerBufferManager.TakeBuffer(bufferSize);
            }
        }
    }
}