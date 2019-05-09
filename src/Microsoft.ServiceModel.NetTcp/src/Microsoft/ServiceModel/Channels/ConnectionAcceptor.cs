using System;
using System.Diagnostics;
using Microsoft.Runtime;
using Microsoft.ServiceModel;
using Microsoft.ServiceModel.Diagnostics;
using Microsoft.ServiceModel.Dispatcher;
using System.Threading;
using Microsoft.Runtime.Diagnostics;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    delegate void ConnectionAvailableCallback(IConnection connection, Action connectionDequeuedCallback);
    delegate void ErrorCallback(Exception exception);

    class ConnectionAcceptor : IDisposable
    {
        int maxAccepts;
        int maxPendingConnections;
        int connections;
        int pendingAccepts;
        IConnectionListener listener;
        Action<Task<IConnection>> handleCompletedAcceptAsync;
        Action onConnectionDequeued;
        bool isDisposed;
        ConnectionAvailableCallback callback;
        ErrorCallback errorCallback;
        AsyncLock asyncLock = new AsyncLock();

        public ConnectionAcceptor(IConnectionListener listener, int maxAccepts, int maxPendingConnections,
            ConnectionAvailableCallback callback)
            : this(listener, maxAccepts, maxPendingConnections, callback, null)
        {
            // empty
        }

        public ConnectionAcceptor(IConnectionListener listener, int maxAccepts, int maxPendingConnections,
            ConnectionAvailableCallback callback, ErrorCallback errorCallback)
        {
            if (maxAccepts <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("maxAccepts", maxAccepts,
                    SR.Format(SR.ValueMustBePositive)));
            }

            Fx.Assert(maxPendingConnections > 0, "maxPendingConnections must be positive");

            this.listener = listener;
            this.maxAccepts = maxAccepts;
            this.maxPendingConnections = maxPendingConnections;
            this.callback = callback;
            this.errorCallback = errorCallback;
            onConnectionDequeued = new Action(OnConnectionDequeued);
            handleCompletedAcceptAsync = Fx.ThunkCallback<Task<IConnection>>(HandleCompletedAcceptAsync);
        }

        bool IsAcceptNecessary
        {
            get
            {
                return (pendingAccepts < maxAccepts)
                    && ((connections + pendingAccepts) < maxPendingConnections)
                    && !isDisposed;
            }
        }

        public int ConnectionCount
        {
            get { return connections; }
        }

        AsyncLock ThisLock
        {
            get { return asyncLock; }
        }

        async void AcceptIfNecessaryAsync(bool startAccepting)
        {
            if (IsAcceptNecessary)
            {
                using (await ThisLock.TakeLockAsync())
                {
                    while (IsAcceptNecessary)
                    {
                        Exception unexpectedException = null;
                        try
                        {
                            var acceptTask = listener.AcceptAsync();
                            // Assigning task to variable to supress warning about not awaiting the task
                            var continuation = acceptTask.ContinueWith(
                                handleCompletedAcceptAsync, 
                                CancellationToken.None, 
                                TaskContinuationOptions.RunContinuationsAsynchronously, // don't block our accept processing loop
                                ActionItem.IOTaskScheduler); // Run the continuation on the IO Thread Scheduler. Protects against thread starvation
                            pendingAccepts++;
                        }
                        catch (CommunicationException exception)
                        {
                            DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
                        }
                        catch (Exception exception)
                        {
                            if (Fx.IsFatal(exception))
                            {
                                throw;
                            }
                            if (startAccepting)
                            {
                                // Since we're under a call to StartAccepting(), just throw the exception up the stack.
                                throw;
                            }
                            if ((errorCallback == null) && !ExceptionHandlerHelper.HandleTransportExceptionHelper(exception))
                            {
                                throw;
                            }
                            unexpectedException = exception;
                        }

                        if ((unexpectedException != null) && (errorCallback != null))
                        {
                            errorCallback(unexpectedException);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            using (ThisLock.TakeLock())
            {
                if (!isDisposed)
                {
                    isDisposed = true;
                    listener.Dispose();
                }
            }
        }

        async void HandleCompletedAcceptAsync(Task<IConnection> antecendant)
        {
            IConnection connection = null;

            using (await ThisLock.TakeLockAsync())
            {
                bool success = false;
                Exception unexpectedException = null;
                try
                {
                    if (!isDisposed)
                    {
                        connection = await antecendant;
                        if (connection != null)
                        {
                            connections++;
                        }
                    }
                    success = true;
                }
                catch (CommunicationException exception)
                {
                    DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
                }
                catch (Exception exception)
                {
                    if (Fx.IsFatal(exception))
                    {
                        throw;
                    }
                    if ((errorCallback == null) && !ExceptionHandlerHelper.HandleTransportExceptionHelper(exception))
                    {
                        throw;
                    }
                    unexpectedException = exception;
                }
                finally
                {
                    if (!success)
                    {
                        connection = null;
                    }
                    pendingAccepts--;
                }

                if ((unexpectedException != null) && (errorCallback != null))
                {
                    errorCallback(unexpectedException);
                }
            }

            AcceptIfNecessaryAsync(false);

            if (connection != null)
            {
                callback(connection, onConnectionDequeued);
            }
        }

        void OnConnectionDequeued()
        {
            using (ThisLock.TakeLock())
            {
                connections--;
            }
            AcceptIfNecessaryAsync(false);
        }

        public void StartAccepting()
        {
            listener.Listen();
            AcceptIfNecessaryAsync(true);
        }
    }

}
