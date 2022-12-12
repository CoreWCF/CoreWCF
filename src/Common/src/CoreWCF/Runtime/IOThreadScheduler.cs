// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Runtime
{
    internal class IOThreadScheduler
    {
        // Do not increase the maximum capacity above 32k!  It must be a power of two, 0x8000 or less, in order to
        // work with the strategy for 'headTail'.
        private const int MaximumCapacity = 0x8000;

        private static class Bits
        {
            public const int HiShift = 32 / 2;

            public const int HiOne = 1 << HiShift;
            public const int LoHiBit = HiOne >> 1;
            public const int HiHiBit = LoHiBit << HiShift;
            public const int LoCountMask = LoHiBit - 1;
            public const int HiCountMask = LoCountMask << HiShift;
            public const int LoMask = LoCountMask | LoHiBit;
            public const int HiMask = HiCountMask | HiHiBit;
            public const int HiBits = LoHiBit | HiHiBit;

            public static int Count(int slot)
            {
                return ((slot >> HiShift) - slot + 2 & LoMask) - 1;
            }

            public static int CountNoIdle(int slot)
            {
                return (slot >> HiShift) - slot + 1 & LoMask;
            }

            public static int IncrementLo(int slot)
            {
                return slot + 1 & LoMask | slot & HiMask;
            }

            // This method is only valid if you already know that (gate & HiBits) != 0.
            public static bool IsComplete(int gate)
            {
                return (gate & HiMask) == gate << HiShift;
            }
        }

        private static IOThreadScheduler s_current = new IOThreadScheduler(32, 32);
        private static SynchronizationContext s_syncContext = new IOThreadSchedulerSynchronizationContext();
        private static TaskScheduler s_IOTaskScheduler;
        private readonly ScheduledOverlapped _overlapped;
        private readonly Slot[] _slots;
        private readonly Slot[] _slotsLowPri;
        private static ThreadLocal<bool> s_isIoThread = new ThreadLocal<bool>();

        // This field holds both the head (HiWord) and tail (LoWord) indices into the slot array.  This limits each
        // value to 64k.  In order to be able to distinguish wrapping the slot array (allowed) from wrapping the
        // indices relative to each other (not allowed), the size of the slot array is limited by an additional bit
        // to 32k.
        //
        // The HiWord (head) holds the index of the last slot to have been scheduled into.  The LoWord (tail) holds
        // the index of the next slot to be dispatched from.  When the queue is empty, the LoWord will be exactly
        // one slot ahead of the HiWord.  When the two are equal, the queue holds one item.
        //
        // When the tail is *two* slots ahead of the head (equivalent to a count of -1), that means the IOTS is
        // idle.  Hence, we start out headTail with a -2 (equivalent) in the head and zero in the tail.
        private int _headTail = -2 << Bits.HiShift;

        // This field is the same except that it governs the low-priority work items.  It doesn't have a concept
        // of idle (-2) so starts empty (-1).
        private int _headTailLowPri = -1 << Bits.HiShift;

        private IOThreadScheduler(int capacity, int capacityLowPri)
        {
            Fx.Assert(capacity > 0, "Capacity must be positive.");
            Fx.Assert(capacity <= 0x8000, "Capacity cannot exceed 32k.");

            Fx.Assert(capacityLowPri > 0, "Low-priority capacity must be positive.");
            Fx.Assert(capacityLowPri <= 0x8000, "Low-priority capacity cannot exceed 32k.");

            _slots = new Slot[capacity];
            Fx.Assert((_slots.Length & SlotMask) == 0, "Capacity must be a power of two.");

            _slotsLowPri = new Slot[capacityLowPri];
            Fx.Assert((_slotsLowPri.Length & SlotMaskLowPri) == 0, "Low-priority capacity must be a power of two.");

            _overlapped = new ScheduledOverlapped();
        }

        public static TaskScheduler IOTaskScheduler
        {
            get
            {
                if (s_IOTaskScheduler == null)
                {
                    var savedCtx = SynchronizationContext.Current;
                    SynchronizationContext.SetSynchronizationContext(s_syncContext);
                    s_IOTaskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                    SynchronizationContext.SetSynchronizationContext(savedCtx);
                }

                return s_IOTaskScheduler;
            }
        }

        public static void ScheduleCallbackNoFlow(Action<object> callback, object state)
        {
            if (callback == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(callback));
            }

            bool queued = false;
            while (!queued)
            {
                try { }
                finally
                {
                    // Called in a finally because it needs to run uninterrupted in order to maintain consistency.
                    queued = s_current.ScheduleCallbackHelper(callback, state);
                }
            }
        }

        public static void ScheduleCallbackLowPriNoFlow(Action<object> callback, object state)
        {
            if (callback == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(callback));
            }

            bool queued = false;
            while (!queued)
            {
                try { }
                finally
                {
                    // Called in a finally because it needs to run uninterrupted in order to maintain consistency.
                    queued = s_current.ScheduleCallbackLowPriHelper(callback, state);
                }
            }
        }

        // Returns true if successfully scheduled, false otherwise.
        private bool ScheduleCallbackHelper(Action<object> callback, object state)
        {
            // See if there's a free slot.  Fortunately the overflow bit is simply lost.
            int slot = Interlocked.Add(ref _headTail, Bits.HiOne);

            // If this brings us to 'empty', then the IOTS used to be 'idle'.  Remember that, and increment
            // again.  This doesn't need to be in a loop, because until we call Post(), we can't go back to idle.
            bool wasIdle = Bits.Count(slot) == 0;
            if (wasIdle)
            {
                slot = Interlocked.Add(ref _headTail, Bits.HiOne);
                Fx.Assert(Bits.Count(slot) != 0, "IOTS went idle when it shouldn't have.");
            }

            // Check if we wrapped *around* to idle.
            if (Bits.Count(slot) == -1)
            {
                // Since the capacity is limited to 32k, this means we wrapped the array at least twice.  That's bad
                // because headTail no longer knows how many work items we have - it looks like zero.  This can
                // only happen if 32k threads come through here while one is swapped out.
                throw Fx.AssertAndThrowFatal("Head/Tail overflow!");
            }

            bool queued = _slots[slot >> Bits.HiShift & SlotMask].TryEnqueueWorkItem(callback, state, out bool wrapped);

            if (wrapped)
            {
                // Wrapped around the circular buffer.  Create a new, bigger IOThreadScheduler.
                IOThreadScheduler next =
                    new IOThreadScheduler(Math.Min(_slots.Length * 2, MaximumCapacity), _slotsLowPri.Length);
                Interlocked.CompareExchange<IOThreadScheduler>(ref s_current, next, this);
            }

            if (wasIdle)
            {
                // It's our responsibility to kick off the overlapped.
                _overlapped.Post(this);
            }

            return queued;
        }

        // Returns true if successfully scheduled, false otherwise.
        private bool ScheduleCallbackLowPriHelper(Action<object> callback, object state)
        {
            // See if there's a free slot.  Fortunately the overflow bit is simply lost.
            int slot = Interlocked.Add(ref _headTailLowPri, Bits.HiOne);

            // If this is the first low-priority work item, make sure we're not idle.
            bool wasIdle = false;
            if (Bits.CountNoIdle(slot) == 1)
            {
                // Since Interlocked calls create a full thread barrier, this will read the value of headTail
                // at the time of the Interlocked.Add or later.  The invariant is that the IOTS is unidle at some
                // point after the Add.
                int ht = _headTail;

                if (Bits.Count(ht) == -1)
                {
                    // Use a temporary local here to store the result of the Interlocked.CompareExchange.  This
                    // works around a codegen bug in the 32-bit JIT (TFS 749182).
                    int interlockedResult = Interlocked.CompareExchange(ref _headTail, ht + Bits.HiOne, ht);
                    if (ht == interlockedResult)
                    {
                        wasIdle = true;
                    }
                }
            }

            // Check if we wrapped *around* to empty.
            if (Bits.CountNoIdle(slot) == 0)
            {
                // Since the capacity is limited to 32k, this means we wrapped the array at least twice.  That's bad
                // because headTail no longer knows how many work items we have - it looks like zero.  This can
                // only happen if 32k threads come through here while one is swapped out.
                throw Fx.AssertAndThrowFatal("Low-priority Head/Tail overflow!");
            }

            bool queued = _slotsLowPri[slot >> Bits.HiShift & SlotMaskLowPri].TryEnqueueWorkItem(
                callback, state, out bool wrapped);

            if (wrapped)
            {
                IOThreadScheduler next =
                    new IOThreadScheduler(_slots.Length, Math.Min(_slotsLowPri.Length * 2, MaximumCapacity));
                Interlocked.CompareExchange<IOThreadScheduler>(ref s_current, next, this);
            }

            if (wasIdle)
            {
                // It's our responsibility to kick off the overlapped.
                _overlapped.Post(this);
            }

            return queued;
        }

        private void CompletionCallback(out Action<object> callback, out object state)
        {
            int slot = _headTail;
            int slotLowPri;
            while (true)
            {
                Fx.Assert(Bits.Count(slot) != -1, "CompletionCallback called on idle IOTS!");

                bool wasEmpty = Bits.Count(slot) == 0;
                if (wasEmpty)
                {
                    // We're about to set this to idle.  First check the low-priority queue.  This alone doesn't
                    // guarantee we service all the low-pri items - there hasn't even been an Interlocked yet.  But
                    // we take care of that later.
                    slotLowPri = _headTailLowPri;
                    while (Bits.CountNoIdle(slotLowPri) != 0)
                    {
                        if (slotLowPri == (slotLowPri = Interlocked.CompareExchange(ref _headTailLowPri,
                            Bits.IncrementLo(slotLowPri), slotLowPri)))
                        {
                            _overlapped.Post(this);
                            _slotsLowPri[slotLowPri & SlotMaskLowPri].DequeueWorkItem(out callback, out state);
                            return;
                        }
                    }
                }

                if (slot == (slot = Interlocked.CompareExchange(ref _headTail, Bits.IncrementLo(slot), slot)))
                {
                    if (!wasEmpty)
                    {
                        _overlapped.Post(this);
                        _slots[slot & SlotMask].DequeueWorkItem(out callback, out state);
                        return;
                    }

                    // We just set the IOThreadScheduler to idle.  Check if a low-priority item got added in the
                    // interim.
                    // Interlocked calls create a thread barrier, so this read will give us the value of
                    // headTailLowPri at the time of the interlocked that set us to idle, or later.  The invariant
                    // here is that either the low-priority queue was empty at some point after we set the IOTS to
                    // idle (so that the next enqueue will notice, and issue a Post), or that the IOTS was unidle at
                    // some point after we set it to idle (so that the next attempt to go idle will verify that the
                    // low-priority queue is empty).
                    slotLowPri = _headTailLowPri;

                    if (Bits.CountNoIdle(slotLowPri) != 0)
                    {
                        // Whoops, go back from being idle (unless someone else already did).  If we go back, start
                        // over.  (We still owe a Post.)
                        slot = Bits.IncrementLo(slot);
                        if (slot == Interlocked.CompareExchange(ref _headTail, slot + Bits.HiOne, slot))
                        {
                            slot += Bits.HiOne;
                            continue;
                        }

                        // We know that there's a low-priority work item.  But we also know that the IOThreadScheduler
                        // wasn't idle.  It's best to let it take care of itself, since according to this method, we
                        // just set the IOThreadScheduler to idle so shouldn't take on any tasks.
                    }

                    break;
                }
            }

            callback = null;
            state = null;
            return;
        }

        private bool TryCoalesce(out Action<object> callback, out object state)
        {
            int slot = _headTail;
            int slotLowPri;
            while (true)
            {
                if (Bits.Count(slot) > 0)
                {
                    if (slot == (slot = Interlocked.CompareExchange(ref _headTail, Bits.IncrementLo(slot), slot)))
                    {
                        _slots[slot & SlotMask].DequeueWorkItem(out callback, out state);
                        return true;
                    }
                    continue;
                }

                slotLowPri = _headTailLowPri;
                if (Bits.CountNoIdle(slotLowPri) > 0)
                {
                    if (slotLowPri == (slotLowPri = Interlocked.CompareExchange(ref _headTailLowPri,
                        Bits.IncrementLo(slotLowPri), slotLowPri)))
                    {
                        _slotsLowPri[slotLowPri & SlotMaskLowPri].DequeueWorkItem(out callback, out state);
                        return true;
                    }
                    slot = _headTail;
                    continue;
                }

                break;
            }

            callback = null;
            state = null;
            return false;
        }

        private int SlotMask
        {
            get
            {
                return _slots.Length - 1;
            }
        }

        private int SlotMaskLowPri
        {
            get
            {
                return _slotsLowPri.Length - 1;
            }
        }

        public static bool IsRunningOnIOThread => s_isIoThread.IsValueCreated && s_isIoThread.Value;

        //TODO, Dev10,607596 cannot apply security critical on finalizer
        //[Fx.Tag.SecurityNote(Critical = "touches slots, may be called outside of user context")]
        //[SecurityCritical]
        ~IOThreadScheduler()
        {
            // If the AppDomain is shutting down, we may still have pending ops.  The AppDomain shutdown will clean
            // everything up.
            if (!Environment.HasShutdownStarted && !AppDomain.CurrentDomain.IsFinalizingForUnload())
            {
#if DEBUG
                DebugVerifyHeadTail();
#endif
                Cleanup();
            }
        }

        private void Cleanup()
        {
            if (_overlapped != null)
            {
                _overlapped.Cleanup();
            }
        }

#if DEBUG
        private void DebugVerifyHeadTail()
        {
            if (_slots != null)
            {
                // The headTail value could technically be zero if the constructor was aborted early.  The
                // constructor wasn't aborted early if the slot array got created.
                Fx.Assert(Bits.Count(_headTail) == -1, "IOTS finalized while not idle.");

                for (int i = 0; i < _slots.Length; i++)
                {
                    _slots[i].DebugVerifyEmpty();
                }
            }

            if (_slotsLowPri != null)
            {
                Fx.Assert(Bits.CountNoIdle(_headTailLowPri) == 0, "IOTS finalized with low-priority items queued.");

                for (int i = 0; i < _slotsLowPri.Length; i++)
                {
                    _slotsLowPri[i].DebugVerifyEmpty();
                }
            }
        }
#endif

        // TryEnqueueWorkItem and DequeueWorkItem use the slot's 'gate' field for synchronization.  Because the
        // slot array is circular and there are no locks, we must assume that multiple threads can be entering each
        // method simultaneously.  If the first DequeueWorkItem occurs before the first TryEnqueueWorkItem, the
        // sequencing (and the enqueue) fails.
        //
        // The gate is a 32-bit int divided into four fields.  The bottom 15 bits (0x00007fff) are the count of
        // threads that have entered TryEnqueueWorkItem.  The first thread to enter is the one responsible for
        // filling the slot with work.  The 16th bit (0x00008000) is a flag indicating that the slot has been
        // successfully filled.  Only the first thread to enter TryEnqueueWorkItem can set this flag.  The
        // high-word (0x7fff0000) is the count of threads entering DequeueWorkItem.  The first thread to enter
        // is the one responsible for accepting (and eventually dispatching) the work in the slot.  The
        // high-bit (0x80000000) is a flag indicating that the slot has been successfully emptied.
        //
        // When the low-word and high-work counters are equal, and both bit flags have been set, the gate is considered
        // 'complete' and can be reset back to zero.  Any operation on the gate might bring it to this state.
        // It's the responsibility of the thread that brings the gate to a completed state to reset it to zero.
        // (It's possible that the gate will fall out of the completed state before it can be reset - that's ok,
        // the next time it becomes completed it can be reset.)
        //
        // It's unlikely either count will ever go higher than 2 or 3.
        //
        // The value of 'callback' has these properties:
        //   -  When the gate is zero, callback is null.
        //   -  When the low-word count is non-zero, but the 0x8000 bit is unset, callback is writable by the thread
        //      that incremented the low word to 1.  Its value is undefined for other threads.  The thread that
        //      sets callback is responsible for setting the 0x8000 bit when it's done.
        //   -  When the 0x8000 bit is set and the high-word count is zero, callback is valid.  (It may be null.)
        //   -  When the 0x8000 bit is set, the high-word count is non-zero, and the high bit is unset, callback is
        //      writable by the thread that incremented the high word to 1 *or* the thread that set the 0x8000 bit,
        //      whichever happened last.  That thread can read the value and set callback to null.  Its value is
        //      undefined for other threads.  The thread that clears the callback is responsible for setting the
        //      high bit.
        //   -  When the high bit is set, callback is null.
        //   -  It's illegal for the gate to be in a state that would satisfy more than one of these conditions.
        //   -  The state field follows the same rules as callback.
        private struct Slot
        {
            private int _gate;
            private Action<object> _callback;
            private object _state;

            public bool TryEnqueueWorkItem(Action<object> callback, object state, out bool wrapped)
            {
                // Register our arrival and check the state of this slot.  If the slot was already full, we wrapped.
                int gateSnapshot = Interlocked.Increment(ref _gate);
                wrapped = (gateSnapshot & Bits.LoCountMask) != 1;
                if (wrapped)
                {
                    if ((gateSnapshot & Bits.LoHiBit) != 0 && Bits.IsComplete(gateSnapshot))
                    {
                        Interlocked.CompareExchange(ref _gate, 0, gateSnapshot);
                    }
                    return false;
                }

                Fx.Assert(_callback == null, "Slot already has a work item.");
                Fx.Assert((gateSnapshot & Bits.HiBits) == 0, "Slot already marked.");

                _state = state;
                _callback = callback;

                // Set the special bit to show that the slot is filled.
                gateSnapshot = Interlocked.Add(ref _gate, Bits.LoHiBit);
                Fx.Assert((gateSnapshot & Bits.HiBits) == Bits.LoHiBit, "Slot already empty.");

                if ((gateSnapshot & Bits.HiCountMask) == 0)
                {
                    // Good - no one has shown up looking for this work yet.
                    return true;
                }

                // Oops - someone already came looking for this work.  We have to abort and reschedule.
                _state = null;
                _callback = null;

                // Indicate that the slot is clear.  We might be able to bypass setting the high bit.
                if (gateSnapshot >> Bits.HiShift != (gateSnapshot & Bits.LoCountMask) ||
                    Interlocked.CompareExchange(ref _gate, 0, gateSnapshot) != gateSnapshot)
                {
                    gateSnapshot = Interlocked.Add(ref _gate, Bits.HiHiBit);
                    if (Bits.IsComplete(gateSnapshot))
                    {
                        Interlocked.CompareExchange(ref _gate, 0, gateSnapshot);
                    }
                }

                return false;
            }

            public void DequeueWorkItem(out Action<object> callback, out object state)
            {
                // Stake our claim on the item.
                int gateSnapshot = Interlocked.Add(ref _gate, Bits.HiOne);

                if ((gateSnapshot & Bits.LoHiBit) == 0)
                {
                    // Whoops, a race.  The work item hasn't made it in yet.  In this context, returning a null callback
                    // is treated like a degenerate work item (rather than an empty queue).  The enqueuing thread will
                    // notice this race and reschedule the real work in a new slot.  Do not reset the slot to zero,
                    // since it's still going to get enqueued into.  (The enqueueing thread will reset it.)
                    callback = null;
                    state = null;
                    return;
                }

                // If we're the first, we get to do the work.
                if ((gateSnapshot & Bits.HiCountMask) == Bits.HiOne)
                {
                    callback = _callback;
                    state = _state;
                    _state = null;
                    _callback = null;

                    // Indicate that the slot is clear.
                    // We should be able to bypass setting the high-bit in the common case.
                    if ((gateSnapshot & Bits.LoCountMask) != 1 ||
                        Interlocked.CompareExchange(ref _gate, 0, gateSnapshot) != gateSnapshot)
                    {
                        gateSnapshot = Interlocked.Add(ref _gate, Bits.HiHiBit);
                        if (Bits.IsComplete(gateSnapshot))
                        {
                            Interlocked.CompareExchange(ref _gate, 0, gateSnapshot);
                        }
                    }
                }
                else
                {
                    callback = null;
                    state = null;

                    // If we're the last, we get to reset the slot.
                    if (Bits.IsComplete(gateSnapshot))
                    {
                        Interlocked.CompareExchange(ref _gate, 0, gateSnapshot);
                    }
                }
            }

#if DEBUG
            public void DebugVerifyEmpty()
            {
                Fx.Assert(_gate == 0, "Finalized with unfinished slot.");
                Fx.Assert(_callback == null, "Finalized with leaked callback.");
                Fx.Assert(_state == null, "Finalized with leaked state.");
            }
#endif
        }

        // A note about the IOThreadScheduler and the ScheduledOverlapped references:
        // Although for each scheduler we have a single instance of overlapped, we cannot point to the scheduler from the
        // overlapped, through the entire lifetime of the overlapped. This is because the ScheduledOverlapped is pinned
        // and if it has a reference to the IOTS, it would be rooted and the finalizer will never get called.
        // Therefore, we are passing the reference, when we post a pending callback and reset it, once the callback was
        // invoked; during that time the scheduler is rooted but in that time we don't want that it would be collected
        // by the GC anyway.
        private unsafe class ScheduledOverlapped
        {
            private static readonly bool s_isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            private readonly NativeOverlapped* _nativeOverlapped;
            private IOThreadScheduler _scheduler;
            private readonly Action _postDelegate;

            public ScheduledOverlapped()
            {
                if (s_isWindows)
                {
                    _nativeOverlapped = (new Overlapped()).UnsafePack(
                        Fx.ThunkCallback(new IOCompletionCallback(IOCallback)), null);
                    _postDelegate = PostIOCP;
                }
                else
                {
                    _postDelegate = PostNewThread;
                }
            }

            private void IOCallback(uint errorCode, uint numBytes, NativeOverlapped* nativeOverlapped)
            {
                Callback();
            }

            private void Callback()
            {
                try
                {
                    InitThreadDebugData();
                    CallbackCore();
                }
                finally
                {
                    ClearThreadDebugData();
                }
            }

            [Conditional("DEBUG")]
            private static void InitThreadDebugData()
            {
                s_isIoThread.Value = true;
                Thread.CurrentThread.Name = "IOThreadScheduler.IOCallback";
            }

            [Conditional("DEBUG")]
            private static void ClearThreadDebugData()
            {
                s_isIoThread.Value = false;
            }

            private void CallbackCore()
            {
                // Unhook the IOThreadScheduler ASAP to prevent it from leaking.
                IOThreadScheduler iots = _scheduler;
                _scheduler = null;
                Fx.Assert(iots != null, "Overlapped completed without a scheduler.");

                Action<object> callback;
                object state;
                try { }
                finally
                {
                    // Called in a finally because it needs to run uninterrupted in order to maintain consistency.
                    iots.CompletionCallback(out callback, out state);
                }

                bool found = true;
                while (found)
                {
                    // The callback can be null if synchronization misses result in unusable slots.  Keep going onto
                    // the next slot in such cases until there are no more slots.
                    if (callback != null)
                    {
                        callback(state);
                    }

                    try { }
                    finally
                    {
                        // Called in a finally because it needs to run uninterrupted in order to maintain consistency.
                        found = iots.TryCoalesce(out callback, out state);
                    }
                }
            }

            public void Post(IOThreadScheduler iots)
            {
                Fx.Assert(_scheduler == null, "Post called on an overlapped that is already posted.");
                Fx.Assert(iots != null, "Post called with a null scheduler.");

                _scheduler = iots;
                _postDelegate();
            }

            private void PostIOCP()
            {
                ThreadPool.UnsafeQueueNativeOverlapped(_nativeOverlapped);
            }

            private void PostNewThread()
            {
                var thread = new Thread(new ThreadStart(Callback));
                thread.Start();
            }

            public void Cleanup()
            {
                if (_scheduler != null)
                {
                    throw Fx.AssertAndThrowFatal("Cleanup called on an overlapped that is in-flight.");
                }

                if (s_isWindows)
                {
                    Overlapped.Free(_nativeOverlapped);
                }
            }
        }

        private class IOThreadSchedulerSynchronizationContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state)
            {
                ScheduleCallbackNoFlow((s) => d(s), state);
            }
        }
    }
}
