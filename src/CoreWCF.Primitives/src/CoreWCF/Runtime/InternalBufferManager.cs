// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;

namespace CoreWCF.Runtime
{
    abstract class InternalBufferManager
    {
        protected InternalBufferManager()
        {
        }

        public abstract byte[] TakeBuffer(int bufferSize);
        public abstract void ReturnBuffer(byte[] buffer);
        public abstract void Clear();

        public static InternalBufferManager Create(long maxBufferPoolSize, int maxBufferSize)
        {
            if (maxBufferPoolSize == 0)
            {
                return GCBufferManager.Value;
            }
            else
            {
                Fx.Assert(maxBufferPoolSize > 0 && maxBufferSize >= 0, "bad params, caller should verify");
                return new PooledBufferManager(maxBufferPoolSize, maxBufferSize);
            }
        }

        class PooledBufferManager : InternalBufferManager
        {
            const int minBufferSize = 128;
            const int maxMissesBeforeTuning = 8;
            const int initialBufferCount = 1;
            readonly object tuningLock;

            int[] bufferSizes;
            BufferPool[] bufferPools;
            long memoryLimit;
            long remainingMemory;
            bool areQuotasBeingTuned;
            int totalMisses;

            public PooledBufferManager(long maxMemoryToPool, int maxBufferSize)
            {
                tuningLock = new object();
                memoryLimit = maxMemoryToPool;
                remainingMemory = maxMemoryToPool;
                List<BufferPool> bufferPoolList = new List<BufferPool>();

                for (int bufferSize = minBufferSize; ;)
                {
                    long bufferCountLong = remainingMemory / bufferSize;

                    int bufferCount = bufferCountLong > int.MaxValue ? int.MaxValue : (int)bufferCountLong;

                    if (bufferCount > initialBufferCount)
                    {
                        bufferCount = initialBufferCount;
                    }

                    bufferPoolList.Add(BufferPool.CreatePool(bufferSize, bufferCount));

                    remainingMemory -= (long)bufferCount * bufferSize;

                    if (bufferSize >= maxBufferSize)
                    {
                        break;
                    }

                    long newBufferSizeLong = (long)bufferSize * 2;

                    if (newBufferSizeLong > (long)maxBufferSize)
                    {
                        bufferSize = maxBufferSize;
                    }
                    else
                    {
                        bufferSize = (int)newBufferSizeLong;
                    }
                }

                bufferPools = bufferPoolList.ToArray();
                bufferSizes = new int[bufferPools.Length];
                for (int i = 0; i < bufferPools.Length; i++)
                {
                    bufferSizes[i] = bufferPools[i].BufferSize;
                }
            }

            public override void Clear()
            {

                for (int i = 0; i < bufferPools.Length; i++)
                {
                    BufferPool bufferPool = bufferPools[i];
                    bufferPool.Clear();
                }
            }

            void ChangeQuota(ref BufferPool bufferPool, int delta)
            {

                //if (TraceCore.BufferPoolChangeQuotaIsEnabled(Fx.Trace))
                //{
                //    TraceCore.BufferPoolChangeQuota(Fx.Trace, bufferPool.BufferSize, delta);
                //}

                BufferPool oldBufferPool = bufferPool;
                int newLimit = oldBufferPool.Limit + delta;
                BufferPool newBufferPool = BufferPool.CreatePool(oldBufferPool.BufferSize, newLimit);
                for (int i = 0; i < newLimit; i++)
                {
                    byte[] buffer = oldBufferPool.Take();
                    if (buffer == null)
                    {
                        break;
                    }
                    newBufferPool.Return(buffer);
                    newBufferPool.IncrementCount();
                }
                remainingMemory -= oldBufferPool.BufferSize * delta;
                bufferPool = newBufferPool;
            }

            void DecreaseQuota(ref BufferPool bufferPool)
            {
                ChangeQuota(ref bufferPool, -1);
            }

            int FindMostExcessivePool()
            {
                long maxBytesInExcess = 0;
                int index = -1;

                for (int i = 0; i < bufferPools.Length; i++)
                {
                    BufferPool bufferPool = bufferPools[i];

                    if (bufferPool.Peak < bufferPool.Limit)
                    {
                        long bytesInExcess = (bufferPool.Limit - bufferPool.Peak) * (long)bufferPool.BufferSize;

                        if (bytesInExcess > maxBytesInExcess)
                        {
                            index = i;
                            maxBytesInExcess = bytesInExcess;
                        }
                    }
                }

                return index;
            }

            int FindMostStarvedPool()
            {
                long maxBytesMissed = 0;
                int index = -1;

                for (int i = 0; i < bufferPools.Length; i++)
                {
                    BufferPool bufferPool = bufferPools[i];

                    if (bufferPool.Peak == bufferPool.Limit)
                    {
                        long bytesMissed = bufferPool.Misses * (long)bufferPool.BufferSize;

                        if (bytesMissed > maxBytesMissed)
                        {
                            index = i;
                            maxBytesMissed = bytesMissed;
                        }
                    }
                }

                return index;
            }

            BufferPool FindPool(int desiredBufferSize)
            {
                for (int i = 0; i < bufferSizes.Length; i++)
                {
                    if (desiredBufferSize <= bufferSizes[i])
                    {
                        return bufferPools[i];
                    }
                }

                return null;
            }

            void IncreaseQuota(ref BufferPool bufferPool)
            {
                ChangeQuota(ref bufferPool, 1);
            }

            public override void ReturnBuffer(byte[] buffer)
            {
                Fx.Assert(buffer != null, "caller must verify");
                BufferPool bufferPool = FindPool(buffer.Length);
                if (bufferPool != null)
                {
                    if (buffer.Length != bufferPool.BufferSize)
                    {
                        throw Fx.Exception.Argument(nameof(buffer), SR.BufferIsNotRightSizeForBufferManager);
                    }

                    if (bufferPool.Return(buffer))
                    {
                        bufferPool.IncrementCount();
                    }
                }
            }

            public override byte[] TakeBuffer(int bufferSize)
            {
                Fx.Assert(bufferSize >= 0, "caller must ensure a non-negative argument");

                BufferPool bufferPool = FindPool(bufferSize);
                byte[] returnValue;
                if (bufferPool != null)
                {
                    byte[] buffer = bufferPool.Take();
                    if (buffer != null)
                    {
                        bufferPool.DecrementCount();
                        returnValue = buffer;
                    }
                    else
                    {
                        if (bufferPool.Peak == bufferPool.Limit)
                        {
                            bufferPool.Misses++;
                            if (++totalMisses >= maxMissesBeforeTuning)
                            {
                                TuneQuotas();
                            }
                        }

                        //if (TraceCore.BufferPoolAllocationIsEnabled(Fx.Trace))
                        //{
                        //    TraceCore.BufferPoolAllocation(Fx.Trace, bufferPool.BufferSize);
                        //}

                        returnValue = Fx.AllocateByteArray(bufferPool.BufferSize);
                    }
                }
                else
                {
                    //if (TraceCore.BufferPoolAllocationIsEnabled(Fx.Trace))
                    //{
                    //    TraceCore.BufferPoolAllocation(Fx.Trace, bufferSize);
                    //}

                    returnValue = Fx.AllocateByteArray(bufferSize);
                }

                return returnValue;
            }

            void TuneQuotas()
            {
                if (areQuotasBeingTuned)
                {
                    return;
                }

                bool lockHeld = false;
                try
                {
                    Monitor.TryEnter(tuningLock, ref lockHeld);

                    // Don't bother if another thread already has the lock
                    if (!lockHeld || areQuotasBeingTuned)
                    {
                        return;
                    }

                    areQuotasBeingTuned = true;
                }
                finally
                {
                    if (lockHeld)
                    {
                        Monitor.Exit(tuningLock);
                    }
                }

                // find the "poorest" pool
                int starvedIndex = FindMostStarvedPool();
                if (starvedIndex >= 0)
                {
                    BufferPool starvedBufferPool = bufferPools[starvedIndex];

                    if (remainingMemory < starvedBufferPool.BufferSize)
                    {
                        // find the "richest" pool
                        int excessiveIndex = FindMostExcessivePool();
                        if (excessiveIndex >= 0)
                        {
                            // steal from the richest
                            DecreaseQuota(ref bufferPools[excessiveIndex]);
                        }
                    }

                    if (remainingMemory >= starvedBufferPool.BufferSize)
                    {
                        // give to the poorest
                        IncreaseQuota(ref bufferPools[starvedIndex]);
                    }
                }

                // reset statistics
                for (int i = 0; i < bufferPools.Length; i++)
                {
                    BufferPool bufferPool = bufferPools[i];
                    bufferPool.Misses = 0;
                }

                totalMisses = 0;
                areQuotasBeingTuned = false;
            }

            abstract class BufferPool
            {
                int bufferSize;
                int count;
                int limit;
                int misses;
                int peak;

                public BufferPool(int bufferSize, int limit)
                {
                    this.bufferSize = bufferSize;
                    this.limit = limit;
                }

                public int BufferSize
                {
                    get { return bufferSize; }
                }

                public int Limit
                {
                    get { return limit; }
                }

                public int Misses
                {
                    get { return misses; }
                    set { misses = value; }
                }

                public int Peak
                {
                    get { return peak; }
                }

                public void Clear()
                {
                    OnClear();
                    count = 0;
                }

                public void DecrementCount()
                {
                    int newValue = count - 1;
                    if (newValue >= 0)
                    {
                        count = newValue;
                    }
                }

                public void IncrementCount()
                {
                    int newValue = count + 1;
                    if (newValue <= limit)
                    {
                        count = newValue;
                        if (newValue > peak)
                        {
                            peak = newValue;
                        }
                    }
                }

                internal abstract byte[] Take();
                internal abstract bool Return(byte[] buffer);
                internal abstract void OnClear();

                internal static BufferPool CreatePool(int bufferSize, int limit)
                {
                    // To avoid many buffer drops during training of large objects which
                    // get allocated on the LOH, we use the LargeBufferPool and for 
                    // bufferSize < 85000, the SynchronizedPool. However if bufferSize < 85000
                    // and (bufferSize + array-overhead) > 85000, this would still use 
                    // the SynchronizedPool even though it is allocated on the LOH.
                    if (bufferSize < 85000)
                    {
                        return new SynchronizedBufferPool(bufferSize, limit);
                    }
                    else
                    {
                        return new LargeBufferPool(bufferSize, limit);
                    }
                }

                class SynchronizedBufferPool : BufferPool
                {
                    SynchronizedPool<byte[]> innerPool;

                    internal SynchronizedBufferPool(int bufferSize, int limit)
                        : base(bufferSize, limit)
                    {
                        innerPool = new SynchronizedPool<byte[]>(limit);
                    }

                    internal override void OnClear()
                    {
                        innerPool.Clear();
                    }

                    internal override byte[] Take()
                    {
                        return innerPool.Take();
                    }

                    internal override bool Return(byte[] buffer)
                    {
                        return innerPool.Return(buffer);
                    }
                }

                class LargeBufferPool : BufferPool
                {
                    Stack<byte[]> items;

                    internal LargeBufferPool(int bufferSize, int limit)
                        : base(bufferSize, limit)
                    {
                        items = new Stack<byte[]>(limit);
                    }

                    object ThisLock
                    {
                        get
                        {
                            return items;
                        }
                    }

                    internal override void OnClear()
                    {
                        lock (ThisLock)
                        {
                            items.Clear();
                        }
                    }

                    internal override byte[] Take()
                    {
                        lock (ThisLock)
                        {
                            if (items.Count > 0)
                            {
                                return items.Pop();
                            }
                        }

                        return null;
                    }

                    internal override bool Return(byte[] buffer)
                    {
                        lock (ThisLock)
                        {
                            if (items.Count < Limit)
                            {
                                items.Push(buffer);
                                return true;
                            }
                        }

                        return false;
                    }
                }
            }
        }

        class GCBufferManager : InternalBufferManager
        {
            static GCBufferManager value = new GCBufferManager();

            GCBufferManager()
            {
            }

            public static GCBufferManager Value
            {
                get { return value; }
            }

            public override void Clear()
            {
            }

            public override byte[] TakeBuffer(int bufferSize)
            {
                return Fx.AllocateByteArray(bufferSize);
            }

            public override void ReturnBuffer(byte[] buffer)
            {
                // do nothing, GC will reclaim this buffer
            }
        }
    }

}