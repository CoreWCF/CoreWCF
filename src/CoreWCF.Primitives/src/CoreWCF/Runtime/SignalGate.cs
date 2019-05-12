using System;
using System.Threading;

namespace CoreWCF.Runtime
{
    class SignalGate
    {
        int state;

        public SignalGate()
        {
        }

        internal bool IsLocked
        {
            get
            {
                return state == GateState.Locked;
            }
        }

        internal bool IsSignalled
        {
            get
            {
                return state == GateState.Signalled;
            }
        }

        // Returns true if this brings the gate to the Signalled state.
        // Transitions - Locked -> SignalPending | Completed before it was unlocked
        //               Unlocked -> Signaled
        public bool Signal()
        {
            int lastState = state;
            if (lastState == GateState.Locked)
            {
                lastState = Interlocked.CompareExchange(ref state, GateState.SignalPending, GateState.Locked);
            }
            if (lastState == GateState.Unlocked)
            {
                state = GateState.Signalled;
                return true;
            }

            if (lastState != GateState.Locked)
            {
                ThrowInvalidSignalGateState();
            }
            return false;
        }

        // Returns true if this brings the gate to the Signalled state.
        // Transitions - SignalPending -> Signaled | return the AsyncResult since the callback already 
        //                                         | completed and provided the result on its thread
        //               Locked -> Unlocked
        public bool Unlock()
        {
            int lastState = state;
            if (lastState == GateState.Locked)
            {
                lastState = Interlocked.CompareExchange(ref state, GateState.Unlocked, GateState.Locked);
            }
            if (lastState == GateState.SignalPending)
            {
                state = GateState.Signalled;
                return true;
            }

            if (lastState != GateState.Locked)
            {
                ThrowInvalidSignalGateState();
            }
            return false;
        }

        // This is factored out to allow Signal and Unlock to be inlined.
        void ThrowInvalidSignalGateState()
        {
            throw Fx.Exception.AsError(new InvalidOperationException(SR.InvalidSemaphoreExit));
        }

        static class GateState
        {
            public const int Locked = 0;
            public const int SignalPending = 1;
            public const int Unlocked = 2;
            public const int Signalled = 3;
        }
    }

    class SignalGate<T> : SignalGate
    {
        T result;

        public SignalGate()
            : base()
        {
        }

        public bool Signal(T result)
        {
            this.result = result;
            return Signal();
        }

        public bool Unlock(out T result)
        {
            if (Unlock())
            {
                result = this.result;
                return true;
            }

            result = default(T);
            return false;
        }
    }
}