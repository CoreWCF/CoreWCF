﻿using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class ReceiveContext
    {
        public readonly static string Name = "ReceiveContext";
        SemaphoreSlim stateLock; // protects state that may be reverted
        bool contextFaulted;
        object thisLock;
        //EventTraceActivity eventTraceActivity;

        protected ReceiveContext()
        {
            thisLock = new object();
            State = ReceiveContextState.Received;
            stateLock = new SemaphoreSlim(1);
        }

        public ReceiveContextState State
        {
            get;
            protected set;
        }

        protected object ThisLock
        {
            get { return thisLock; }
        }

        public event EventHandler Faulted;

        public static bool TryGet(Message message, out ReceiveContext property)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            bool result = TryGet(message.Properties, out property);
            //if (result && FxTrace.Trace.IsEnd2EndActivityTracingEnabled && property.eventTraceActivity == null)
            //{
            //    property.eventTraceActivity = EventTraceActivityHelper.TryExtractActivity(message);
            //}

            return result;
        }

        public static bool TryGet(MessageProperties properties, out ReceiveContext property)
        {
            if (properties == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(properties));
            }

            property = null;
            object foundProperty;
            if (properties.TryGetValue(Name, out foundProperty))
            {
                property = (ReceiveContext)foundProperty;
                return true;
            }
            return false;
        }

        public virtual void Abandon(TimeSpan timeout)
        {
            Abandon(null, timeout);
        }

        public virtual void Abandon(Exception exception, TimeSpan timeout)
        {
            EnsureValidTimeout(timeout);
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            WaitForStateLock(timeoutHelper.RemainingTime());

            try
            {
                if (PreAbandon())
                {
                    return;
                }
            }
            finally
            {
                // Abandon can never be reverted, release the state lock.
                ReleaseStateLock();
            }

            bool success = false;
            try
            {
                if (exception == null)
                {
                    OnAbandon(timeoutHelper.RemainingTime());
                }
                else
                {
                    //if (TD.ReceiveContextAbandonWithExceptionIsEnabled())
                    //{
                    //    TD.ReceiveContextAbandonWithException(this.eventTraceActivity, this.GetType().ToString(), exception.GetType().ToString());
                    //}
                    OnAbandon(exception, timeoutHelper.RemainingTime());
                }
                lock (ThisLock)
                {
                    ThrowIfFaulted();
                    ThrowIfNotAbandoning();
                    State = ReceiveContextState.Abandoned;
                }
                success = true;
            }
            finally
            {
                if (!success)
                {
                    //if (TD.ReceiveContextAbandonFailedIsEnabled())
                    //{
                    //    TD.ReceiveContextAbandonFailed(this.eventTraceActivity, this.GetType().ToString());
                    //}
                    Fault();
                }
            }
        }

        public virtual Task AbandonAsync(CancellationToken token)
        {
            return AbandonAsync(null, token);
        }

        public virtual async Task AbandonAsync(Exception exception, CancellationToken token)
        {
            await WaitForStateLockAsync(token);

            try
            {
                if (PreAbandon())
                {
                    return;
                }
            }
            finally
            {
                // Abandon can never be reverted, release the state lock.
                ReleaseStateLock();
            }

            bool success = false;
            try
            {
                if (exception == null)
                {
                    await OnAbandonAsync(token);
                }
                else
                {
                    //if (TD.ReceiveContextAbandonWithExceptionIsEnabled())
                    //{
                    //    TD.ReceiveContextAbandonWithException(this.eventTraceActivity, this.GetType().ToString(), exception.GetType().ToString());
                    //}
                    await OnAbandonAsync(exception, token);
                }
                lock (ThisLock)
                {
                    ThrowIfFaulted();
                    ThrowIfNotAbandoning();
                    State = ReceiveContextState.Abandoned;
                }
                success = true;
            }
            finally
            {
                if (!success)
                {
                    //if (TD.ReceiveContextAbandonFailedIsEnabled())
                    //{
                    //    TD.ReceiveContextAbandonFailed(this.eventTraceActivity, this.GetType().ToString());
                    //}
                    Fault();
                }
            }
        }

        public virtual async Task CompleteAsync(CancellationToken token)
        {
            await WaitForStateLockAsync(token);
            bool success = false;

            try
            {
                PreComplete();
                success = true;
            }
            finally
            {
                // Case 1: State validation fails, release the lock.
                // Case 2: No transaction, the state can never be reverted, release the lock.
                // Case 3: Transaction, keep the lock until we know the transaction outcome (OnTransactionStatusNotification).
                if (!success /*|| Transaction.Current == null*/)
                {
                    ReleaseStateLock();
                }
            }

            success = false;
            try
            {
                await OnCompleteAsync(token);
                lock (ThisLock)
                {
                    ThrowIfFaulted();
                    ThrowIfNotCompleting();
                    State = ReceiveContextState.Completed;
                }
                success = true;
            }
            finally
            {
                if (!success)
                {
                    //if (TD.ReceiveContextCompleteFailedIsEnabled())
                    //{
                    //    TD.ReceiveContextCompleteFailed(this.eventTraceActivity, this.GetType().ToString());
                    //}
                    Fault();
                }
            }

        }

        public virtual void Complete(TimeSpan timeout)
        {
            EnsureValidTimeout(timeout);
            TimeoutHelper timeoutHelper = new TimeoutHelper(timeout);
            WaitForStateLock(timeoutHelper.RemainingTime());
            bool success = false;

            try
            {
                PreComplete();
                success = true;
            }
            finally
            {
                // Case 1: State validation fails, release the lock.
                // Case 2: No transaction, the state can never be reverted, release the lock.
                // Case 3: Transaction, keep the lock until we know the transaction outcome (OnTransactionStatusNotification).
                if (!success /*|| Transaction.Current == null*/)
                {
                    ReleaseStateLock();
                }
            }

            success = false;
            try
            {
                OnComplete(timeoutHelper.RemainingTime());
                lock (ThisLock)
                {
                    ThrowIfFaulted();
                    ThrowIfNotCompleting();
                    State = ReceiveContextState.Completed;
                }
                success = true;
            }
            finally
            {
                if (!success)
                {
                    //if (TD.ReceiveContextCompleteFailedIsEnabled())
                    //{
                    //    TD.ReceiveContextCompleteFailed(this.eventTraceActivity, this.GetType().ToString());
                    //}
                    Fault();
                }
            }
        }

        void EnsureValidTimeout(TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(timeout), SR.SFxTimeoutOutOfRange0));
            }

            if (TimeoutHelper.IsTooLarge(timeout))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(timeout), timeout, SR.SFxTimeoutOutOfRangeTooBig));
            }
        }

        protected internal virtual void Fault()
        {
            lock (ThisLock)
            {
                if (State == ReceiveContextState.Completed || State == ReceiveContextState.Abandoned || State == ReceiveContextState.Faulted)
                {
                    return;
                }
                State = ReceiveContextState.Faulted;
            }
            OnFaulted();
        }

        protected abstract void OnAbandon(TimeSpan timeout);
        protected virtual void OnAbandon(Exception exception, TimeSpan timeout)
        {
            // default implementation: delegate to non-exception overload, ignoring reason
            OnAbandon(timeout);
        }

        protected abstract Task OnAbandonAsync(CancellationToken token);

        protected virtual Task OnAbandonAsync(Exception exception, CancellationToken token)
        {
            // default implementation: delegate to non-exception overload, ignoring reason
            return OnAbandonAsync(token);
        }

        protected abstract Task OnCompleteAsync(CancellationToken token);
        protected abstract void OnComplete(TimeSpan timeout);

        protected virtual void OnFaulted()
        {
            lock (ThisLock)
            {
                if (contextFaulted)
                {
                    return;
                }
                contextFaulted = true;
            }

            //if (TD.ReceiveContextFaultedIsEnabled())
            //{
            //    TD.ReceiveContextFaulted(this.eventTraceActivity, this);
            //}

            EventHandler handler = Faulted;

            if (handler != null)
            {
                try
                {
                    handler(this, EventArgs.Empty);
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                    {
                        throw;
                    }

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(exception);
                }
            }
        }

        //void OnTransactionStatusNotification(TransactionStatus status)
        //{
        //    lock (ThisLock)
        //    {
        //        if (status == TransactionStatus.Aborted)
        //        {
        //            if (this.State == ReceiveContextState.Completing || this.State == ReceiveContextState.Completed)
        //            {
        //                this.State = ReceiveContextState.Received;
        //            }
        //        }
        //    }

        //    if (status != TransactionStatus.Active)
        //    {
        //        this.ReleaseStateLock();
        //    }
        //}

        bool PreAbandon()
        {
            bool alreadyAbandoned = false;
            lock (ThisLock)
            {
                if (State == ReceiveContextState.Abandoning || State == ReceiveContextState.Abandoned)
                {
                    alreadyAbandoned = true;
                }
                else
                {
                    ThrowIfFaulted();
                    ThrowIfNotReceived();
                    State = ReceiveContextState.Abandoning;
                }
            }
            return alreadyAbandoned;
        }

        void PreComplete()
        {
            lock (ThisLock)
            {
                ThrowIfFaulted();
                ThrowIfNotReceived();
                //if (Transaction.Current != null)
                //{
                //    Transaction.Current.EnlistVolatile(new EnlistmentNotifications(this), EnlistmentOptions.None);
                //}
                State = ReceiveContextState.Completing;
            }
        }

        void ReleaseStateLock()
        {
            stateLock.Release();
        }

        void ThrowIfFaulted()
        {

            if (State == ReceiveContextState.Faulted)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new CommunicationException(SR.Format(SR.ReceiveContextFaulted, GetType().ToString())));
            }
        }

        void ThrowIfNotAbandoning()
        {
            if (State != ReceiveContextState.Abandoning)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new InvalidOperationException(SR.Format(SR.ReceiveContextInInvalidState, GetType().ToString(), State.ToString())));
            }
        }

        void ThrowIfNotCompleting()
        {
            if (State != ReceiveContextState.Completing)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new InvalidOperationException(SR.Format(SR.ReceiveContextInInvalidState, GetType().ToString(), State.ToString())));
            }
        }

        void ThrowIfNotReceived()
        {
            if (State != ReceiveContextState.Received)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new InvalidOperationException(SR.Format(SR.ReceiveContextCannotBeUsed, GetType().ToString(), State.ToString())));
            }
        }

        async Task WaitForStateLockAsync(CancellationToken token)
        {
            try
            {
                await stateLock.WaitAsync(token);
            }
            catch (TaskCanceledException exception)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(WrapStateException(exception));
            }
        }

        void WaitForStateLock(TimeSpan timeout)
        {
            try
            {
                stateLock.Wait(timeout);
            }
            catch (TimeoutException exception)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(WrapStateException(exception));
            }
        }

        Exception WrapStateException(Exception exception)
        {
            return new InvalidOperationException(SR.Format(SR.ReceiveContextInInvalidState, GetType().ToString(), State.ToString()), exception);
        }
    }
}