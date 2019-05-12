﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using CoreWCF.Runtime;
using CoreWCF.Diagnostics;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    // TODO: Go through and verify all the internal methods to see if they are needed/used
    public abstract class CommunicationObject : ICommunicationObject
    {
        bool _aborted;
        bool _closeCalled;
        ExceptionQueue _exceptionQueue;
        object _mutex;
        bool _onClosingCalled;
        bool _onClosedCalled;
        bool _onOpeningCalled;
        bool _onOpenedCalled;
        bool _raisedClosed;
        bool _raisedClosing;
        bool _raisedFaulted;
        //bool traceOpenAndClose;
        object _eventSender;
        CommunicationState _state;

        protected CommunicationObject() : this(new object()) { }

        protected CommunicationObject(object mutex)
        {
            _mutex = mutex;
            _eventSender = this;
            _state = CommunicationState.Created;
        }

        internal bool Aborted
        {
            get { return _aborted; }
        }

        internal object EventSender
        {
            get { return _eventSender; }
            set { _eventSender = value; }
        }

        protected bool IsDisposed
        {
            get { return _state == CommunicationState.Closed; }
        }

        public CommunicationState State
        {
            get { return _state; }
        }

        protected object ThisLock
        {
            get { return _mutex; }
        }

        protected abstract TimeSpan DefaultCloseTimeout { get; }
        protected abstract TimeSpan DefaultOpenTimeout { get; }

        internal TimeSpan InternalCloseTimeout
        {
            get { return DefaultCloseTimeout; }
        }

        internal TimeSpan InternalOpenTimeout
        {
            get { return DefaultOpenTimeout; }
        }

        public event EventHandler Closed;
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;

        public void Abort()
        {
            lock (ThisLock)
            {
                if (_aborted || _state == CommunicationState.Closed)
                    return;
                _aborted = true;
                _state = CommunicationState.Closing;
            }

            //if (DiagnosticUtility.ShouldTraceInformation)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.CommunicationObjectAborted, SR.Format(SR.TraceCodeCommunicationObjectAborted, TraceUtility.CreateSourceString(this)), this);
            //}

            //bool throwing = true;

            //try
            //{
                OnClosing();
                if (!_onClosingCalled)
                    throw TraceUtility.ThrowHelperError(CreateBaseClassMethodNotCalledException("OnClosing"), Guid.Empty, this);

                OnAbort();

                OnClosed();
                if (!_onClosedCalled)
                    throw TraceUtility.ThrowHelperError(CreateBaseClassMethodNotCalledException("OnClosed"), Guid.Empty, this);

                //throwing = false;
            //}
            //finally
            //{
            //    if (throwing)
            //    {
            //        if (DiagnosticUtility.ShouldTraceWarning)
            //            TraceUtility.TraceEvent(TraceEventType.Warning, TraceCode.CommunicationObjectAbortFailed, SR.Format(SR.TraceCodeCommunicationObjectAbortFailed, this.GetCommunicationObjectType().ToString()), this);
            //    }
            //}
        }

        public Task CloseAsync()
        {
            var helper = new TimeoutHelper(DefaultCloseTimeout);
            return CloseAsync(helper.GetCancellationToken());
        }

        public async Task CloseAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            //using (DiagnosticUtility.ShouldUseActivity && this.TraceOpenAndClose ? this.CreateCloseActivity() : null)
            //{

                CommunicationState originalState;
                lock (ThisLock)
                {
                    originalState = _state;
                    if (originalState != CommunicationState.Closed)
                        _state = CommunicationState.Closing;

                    _closeCalled = true;
                }

                switch (originalState)
                {
                    case CommunicationState.Created:
                    case CommunicationState.Opening:
                    case CommunicationState.Faulted:
                        Abort();
                        if (originalState == CommunicationState.Faulted)
                        {
                            throw TraceUtility.ThrowHelperError(CreateFaultedException(), Guid.Empty, this);
                        }
                        break;

                    case CommunicationState.Opened:
                        {
                            bool throwing = true;
                            try
                            {
                                //TimeoutHelper actualTimeout = new TimeoutHelper(timeout);

                                OnClosing();
                                if (!_onClosingCalled)
                                    throw TraceUtility.ThrowHelperError(CreateBaseClassMethodNotCalledException("OnClosing"), Guid.Empty, this);

                                await OnCloseAsync(token);

                                OnClosed();
                                if (!_onClosedCalled)
                                    throw TraceUtility.ThrowHelperError(CreateBaseClassMethodNotCalledException("OnClosed"), Guid.Empty, this);

                                throwing = false;
                            }
                            finally
                            {
                                if (throwing)
                                {
                                    //if (DiagnosticUtility.ShouldTraceWarning)
                                    //{
                                    //    TraceUtility.TraceEvent(TraceEventType.Warning, TraceCode.CommunicationObjectCloseFailed, SR.Format(SR.TraceCodeCommunicationObjectCloseFailed, this.GetCommunicationObjectType().ToString()), this);
                                    //}

                                    Abort();
                                }
                            }
                            break;
                        }

                    case CommunicationState.Closing:
                    case CommunicationState.Closed:
                        break;

                    default:
                        throw Fx.AssertAndThrow("CommunicationObject.BeginClose: Unknown CommunicationState");
                }
            //}

        }

        public System.Threading.Tasks.Task OpenAsync()
        {
            // TODO: Switch to TimeoutHelper from client
            var cts = new CancellationTokenSource();
            cts.CancelAfter(DefaultCloseTimeout);
            return OpenAsync(cts.Token);
        }

        public async Task OpenAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            //using (ServiceModelActivity activity = DiagnosticUtility.ShouldUseActivity && this.TraceOpenAndClose ? ServiceModelActivity.CreateBoundedActivity() : null)
            //{
                //if (DiagnosticUtility.ShouldUseActivity)
                //{
                //    ServiceModelActivity.Start(activity, this.OpenActivityName, this.OpenActivityType);
                //}
                lock (ThisLock)
                {
                    ThrowIfDisposedOrImmutable();
                    _state = CommunicationState.Opening;
                }

                bool throwing = true;
                try
                {
                    OnOpening();
                    if (!_onOpeningCalled)
                        throw TraceUtility.ThrowHelperError(CreateBaseClassMethodNotCalledException("OnOpening"), Guid.Empty, this);

                    await OnOpenAsync(token);

                    OnOpened();
                    if (!_onOpenedCalled)
                        throw TraceUtility.ThrowHelperError(CreateBaseClassMethodNotCalledException("OnOpened"), Guid.Empty, this);

                    throwing = false;
                }
                finally
                {
                    if (throwing)
                    {
                        //if (DiagnosticUtility.ShouldTraceWarning)
                        //{
                        //    TraceUtility.TraceEvent(TraceEventType.Warning, TraceCode.CommunicationObjectOpenFailed, SR.Format(SR.TraceCodeCommunicationObjectOpenFailed, this.GetCommunicationObjectType().ToString()), this);
                        //}

                        Fault();
                    }
                }
            //}
        }

        // TODO: Make internal again
        public void Fault(Exception exception)
        {
            lock (ThisLock)
            {
                if (_exceptionQueue == null)
                    _exceptionQueue = new ExceptionQueue(ThisLock);
            }

            //if (exception != null && DiagnosticUtility.ShouldTraceInformation)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.CommunicationObjectFaultReason,
            //        SR.TraceCodeCommunicationObjectFaultReason, exception, null);
            //}

            _exceptionQueue.AddException(exception);
            Fault();
        }

        internal void AddPendingException(Exception exception)
        {
            lock (ThisLock)
            {
                if (_exceptionQueue == null)
                    _exceptionQueue = new ExceptionQueue(ThisLock);
            }

            _exceptionQueue.AddException(exception);
        }

        internal Exception GetPendingException()
        {
            CommunicationState currentState = _state;

            Fx.Assert(currentState == CommunicationState.Closing || currentState == CommunicationState.Closed || currentState == CommunicationState.Faulted,
                "CommunicationObject.GetPendingException(currentState == CommunicationState.Closing || currentState == CommunicationState.Closed || currentState == CommunicationState.Faulted)");

            ExceptionQueue queue = _exceptionQueue;
            if (queue != null)
            {
                return queue.GetException();
            }
            else
            {
                return null;
            }
        }

        protected void Fault()
        {
            lock (ThisLock)
            {
                if (_state == CommunicationState.Closed || _state == CommunicationState.Closing)
                    return;

                if (_state == CommunicationState.Faulted)
                    return;

                _state = CommunicationState.Faulted;
            }

            OnFaulted();
        }

        internal void ThrowIfClosed()
        {
            ThrowPending();

            switch (_state)
            {
                case CommunicationState.Created:
                    break;

                case CommunicationState.Opening:
                    break;

                case CommunicationState.Opened:
                    break;

                case CommunicationState.Closing:
                    break;

                case CommunicationState.Closed:
                    throw TraceUtility.ThrowHelperError(CreateClosedException(), Guid.Empty, this);

                case CommunicationState.Faulted:
                    throw TraceUtility.ThrowHelperError(CreateFaultedException(), Guid.Empty, this);

                default:
                    throw Fx.AssertAndThrow("ThrowIfClosed: Unknown CommunicationObject.state");
            }
        }

        protected virtual Type GetCommunicationObjectType()
        {
            return GetType();
        }

        protected abstract void OnAbort();

        protected abstract Task OnCloseAsync(CancellationToken token);

        protected abstract Task OnOpenAsync(CancellationToken token);

        protected virtual void OnClosed()
        {
            _onClosedCalled = true;

            lock (ThisLock)
            {
                if (_raisedClosed)
                    return;
                _raisedClosed = true;
                _state = CommunicationState.Closed;
            }

            //if (DiagnosticUtility.ShouldTraceVerbose)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Verbose, TraceCode.CommunicationObjectClosed, SR.Format(SR.TraceCodeCommunicationObjectClosed, TraceUtility.CreateSourceString(this)), this);
            //}

            EventHandler handler = Closed;
            if (handler != null)
            {
                try
                {
                    handler(_eventSender, EventArgs.Empty);
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                        throw;

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(exception);
                }
            }
        }

        protected virtual void OnClosing()
        {
            _onClosingCalled = true;

            lock (ThisLock)
            {
                if (_raisedClosing)
                    return;
                _raisedClosing = true;
            }

            //if (DiagnosticUtility.ShouldTraceVerbose)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Verbose, TraceCode.CommunicationObjectClosing, SR.Format(SR.TraceCodeCommunicationObjectClosing, TraceUtility.CreateSourceString(this)), this);
            //}
            EventHandler handler = Closing;
            if (handler != null)
            {
                try
                {
                    handler(_eventSender, EventArgs.Empty);
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                        throw;

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(exception);
                }
            }
        }

        protected virtual void OnFaulted()
        {
            lock (ThisLock)
            {
                if (_raisedFaulted)
                    return;
                _raisedFaulted = true;
            }

            //if (DiagnosticUtility.ShouldTraceWarning)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Warning, TraceCode.CommunicationObjectFaulted, SR.Format(SR.TraceCodeCommunicationObjectFaulted, this.GetCommunicationObjectType().ToString()), this);
            //}

            EventHandler handler = Faulted;
            if (handler != null)
            {
                try
                {
                    handler(_eventSender, EventArgs.Empty);
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                        throw;

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(exception);
                }
            }
        }

        protected virtual void OnOpened()
        {
            _onOpenedCalled = true;

            lock (ThisLock)
            {
                if (_aborted || _state != CommunicationState.Opening)
                    return;
                _state = CommunicationState.Opened;
            }

            //if (DiagnosticUtility.ShouldTraceVerbose)
            //    TraceUtility.TraceEvent(TraceEventType.Verbose, TraceCode.CommunicationObjectOpened, SR.Format(SR.TraceCodeCommunicationObjectOpened, TraceUtility.CreateSourceString(this)), this);

            EventHandler handler = Opened;
            if (handler != null)
            {
                try
                {
                    handler(_eventSender, EventArgs.Empty);
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                        throw;

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(exception);
                }
            }
        }

        protected virtual void OnOpening()
        {
            _onOpeningCalled = true;

            //if (DiagnosticUtility.ShouldTraceVerbose)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Verbose, TraceCode.CommunicationObjectOpening, SR.Format(SR.TraceCodeCommunicationObjectOpening, TraceUtility.CreateSourceString(this)), this);
            //}

            EventHandler handler = Opening;
            if (handler != null)
            {
                try
                {
                    handler(_eventSender, EventArgs.Empty);
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                        throw;

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(exception);
                }
            }
        }
        internal void ThrowIfFaulted()
        {
            ThrowPending();

            switch (_state)
            {
                case CommunicationState.Created:
                    break;

                case CommunicationState.Opening:
                    break;

                case CommunicationState.Opened:
                    break;

                case CommunicationState.Closing:
                    break;

                case CommunicationState.Closed:
                    break;

                case CommunicationState.Faulted:
                    throw TraceUtility.ThrowHelperError(CreateFaultedException(), Guid.Empty, this);

                default:
                    throw Fx.AssertAndThrow("ThrowIfFaulted: Unknown CommunicationObject.state");
            }
        }

        internal void ThrowIfAborted()
        {
            if (_aborted && !_closeCalled)
            {
                throw TraceUtility.ThrowHelperError(CreateAbortedException(), Guid.Empty, this);
            }
        }

        Exception CreateNotOpenException()
        {
            return new InvalidOperationException(SR.Format(SR.CommunicationObjectCannotBeUsed, GetCommunicationObjectType().ToString(), _state.ToString()));
        }

        Exception CreateBaseClassMethodNotCalledException(string method)
        {
            return new InvalidOperationException(SR.Format(SR.CommunicationObjectBaseClassMethodNotCalled, GetCommunicationObjectType().ToString(), method));
        }

        Exception CreateImmutableException()
        {
            return new InvalidOperationException(SR.Format(SR.CommunicationObjectCannotBeModifiedInState, GetCommunicationObjectType().ToString(), _state.ToString()));
        }

        internal Exception CreateClosedException()
        {
            if (!_closeCalled)
            {
                return CreateAbortedException();
            }
            else
            {
                return new ObjectDisposedException(GetCommunicationObjectType().ToString());
            }
        }

        internal Exception CreateFaultedException()
        {
            string message = SR.Format(SR.CommunicationObjectFaulted1, GetCommunicationObjectType().ToString());
            return new CommunicationObjectFaultedException(message);
        }

        internal Exception CreateAbortedException()
        {
            return new CommunicationObjectAbortedException(SR.Format(SR.CommunicationObjectAborted1, GetCommunicationObjectType().ToString()));
        }

        protected internal void ThrowIfDisposed()
        {
            ThrowPending();

            switch (_state)
            {
                case CommunicationState.Created:
                    break;

                case CommunicationState.Opening:
                    break;

                case CommunicationState.Opened:
                    break;

                case CommunicationState.Closing:
                    throw TraceUtility.ThrowHelperError(CreateClosedException(), Guid.Empty, this);

                case CommunicationState.Closed:
                    throw TraceUtility.ThrowHelperError(CreateClosedException(), Guid.Empty, this);

                case CommunicationState.Faulted:
                    throw TraceUtility.ThrowHelperError(CreateFaultedException(), Guid.Empty, this);

                default:
                    throw Fx.AssertAndThrow("ThrowIfDisposed: Unknown CommunicationObject.state");
            }
        }

        internal void ThrowIfClosedOrOpened()
        {
            ThrowPending();

            switch (_state)
            {
                case CommunicationState.Created:
                    break;

                case CommunicationState.Opening:
                    break;

                case CommunicationState.Opened:
                    throw TraceUtility.ThrowHelperError(CreateImmutableException(), Guid.Empty, this);

                case CommunicationState.Closing:
                    throw TraceUtility.ThrowHelperError(CreateImmutableException(), Guid.Empty, this);

                case CommunicationState.Closed:
                    throw TraceUtility.ThrowHelperError(CreateClosedException(), Guid.Empty, this);

                case CommunicationState.Faulted:
                    throw TraceUtility.ThrowHelperError(CreateFaultedException(), Guid.Empty, this);

                default:
                    throw Fx.AssertAndThrow("ThrowIfClosedOrOpened: Unknown CommunicationObject.state");
            }
        }

        protected internal void ThrowIfDisposedOrImmutable()
        {
            ThrowPending();

            switch (_state)
            {
                case CommunicationState.Created:
                    break;

                case CommunicationState.Opening:
                    throw TraceUtility.ThrowHelperError(CreateImmutableException(), Guid.Empty, this);

                case CommunicationState.Opened:
                    throw TraceUtility.ThrowHelperError(CreateImmutableException(), Guid.Empty, this);

                case CommunicationState.Closing:
                    throw TraceUtility.ThrowHelperError(CreateClosedException(), Guid.Empty, this);

                case CommunicationState.Closed:
                    throw TraceUtility.ThrowHelperError(CreateClosedException(), Guid.Empty, this);

                case CommunicationState.Faulted:
                    throw TraceUtility.ThrowHelperError(CreateFaultedException(), Guid.Empty, this);

                default:
                    throw Fx.AssertAndThrow("ThrowIfDisposedOrImmutable: Unknown CommunicationObject.state");
            }
        }

        protected internal void ThrowIfDisposedOrNotOpen()
        {
            ThrowPending();

            switch (_state)
            {
                case CommunicationState.Created:
                    throw TraceUtility.ThrowHelperError(CreateNotOpenException(), Guid.Empty, this);

                case CommunicationState.Opening:
                    throw TraceUtility.ThrowHelperError(CreateNotOpenException(), Guid.Empty, this);

                case CommunicationState.Opened:
                    break;

                case CommunicationState.Closing:
                    throw TraceUtility.ThrowHelperError(CreateClosedException(), Guid.Empty, this);

                case CommunicationState.Closed:
                    throw TraceUtility.ThrowHelperError(CreateClosedException(), Guid.Empty, this);

                case CommunicationState.Faulted:
                    throw TraceUtility.ThrowHelperError(CreateFaultedException(), Guid.Empty, this);

                default:
                    throw Fx.AssertAndThrow("ThrowIfDisposedOrNotOpen: Unknown CommunicationObject.state");
            }
        }

        public void ThrowIfNotOpened()
        {
            if (_state == CommunicationState.Created || _state == CommunicationState.Opening)
                throw TraceUtility.ThrowHelperError(CreateNotOpenException(), Guid.Empty, this);
        }

        internal void ThrowIfClosedOrNotOpen()
        {
            ThrowPending();

            switch (_state)
            {
                case CommunicationState.Created:
                    throw TraceUtility.ThrowHelperError(CreateNotOpenException(), Guid.Empty, this);

                case CommunicationState.Opening:
                    throw TraceUtility.ThrowHelperError(CreateNotOpenException(), Guid.Empty, this);

                case CommunicationState.Opened:
                    break;

                case CommunicationState.Closing:
                    break;

                case CommunicationState.Closed:
                    throw TraceUtility.ThrowHelperError(CreateClosedException(), Guid.Empty, this);

                case CommunicationState.Faulted:
                    throw TraceUtility.ThrowHelperError(CreateFaultedException(), Guid.Empty, this);

                default:
                    throw Fx.AssertAndThrow("ThrowIfClosedOrNotOpen: Unknown CommunicationObject.state");
            }
        }

        //TODO: Make internal again
        public void ThrowPending()
        {
            ExceptionQueue queue = _exceptionQueue;

            if (queue != null)
            {
                Exception exception = queue.GetException();

                if (exception != null)
                {
                    throw TraceUtility.ThrowHelperError(exception, Guid.Empty, this);
                }
            }
        }

        class ExceptionQueue
        {
            Queue<Exception> _exceptions = new Queue<Exception>();
            object _thisLock;

            internal ExceptionQueue(object thisLock)
            {
                _thisLock = thisLock;
            }

            object ThisLock
            {
                get { return _thisLock; }
            }

            public void AddException(Exception exception)
            {
                if (exception == null)
                {
                    return;
                }

                lock (ThisLock)
                {
                    _exceptions.Enqueue(exception);
                }
            }

            public Exception GetException()
            {
                lock (ThisLock)
                {
                    if (_exceptions.Count > 0)
                    {
                        return _exceptions.Dequeue();
                    }
                }

                return null;
            }
        }
    }
}