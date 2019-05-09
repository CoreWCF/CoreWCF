using Microsoft.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ServiceModel.Channels
{
    abstract class TransportManager
    {
        int openCount;
        AsyncLock thisLock = new AsyncLock();

        internal abstract string Scheme { get; }

        internal AsyncLock ThisLock
        {
            get { return thisLock; }
        }

        internal Task CloseAsync(TransportChannelListener channelListener, CancellationToken token)
        {
            return CleanupAsync(channelListener, false, token);
        }

        Task CleanupAsync(TransportChannelListener channelListener, bool aborting, CancellationToken token)
        {
            Unregister(channelListener);
            lock (ThisLock)
            {
                if (openCount <= 0)
                {
                    throw Fx.AssertAndThrow("Invalid Open/Close state machine.");
                }

                openCount--;

                if (openCount == 0)
                {
                    // Wrap the final close here with transfers.
                    if (aborting)
                    {
                        OnAbort();
                        return Task.CompletedTask;
                    }
                    else
                    {
                        return OnCloseAsync(token);
                    }
                }
            }

            return Task.CompletedTask;
        }

        internal static void EnsureRegistered<TChannelListener>(UriPrefixTable<TChannelListener> addressTable,
            TChannelListener channelListener, HostNameComparisonMode registeredComparisonMode)
            where TChannelListener : TransportChannelListener
        {
            TChannelListener existingFactory;
            if (!addressTable.TryLookupUri(channelListener.Uri, registeredComparisonMode, out existingFactory) ||
                (existingFactory != channelListener))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(
                    SR.ListenerFactoryNotRegistered, channelListener.Uri)));
            }
        }

        // Must be called under lock(ThisLock).
        protected void Fault<TChannelListener>(UriPrefixTable<TChannelListener> addressTable, Exception exception)
            where TChannelListener : ChannelListenerBase
        {
            foreach (KeyValuePair<BaseUriWithWildcard, TChannelListener> pair in addressTable.GetAll())
            {
                TChannelListener listener = pair.Value;
                listener.Fault(exception);
                listener.Abort();
            }
        }

        internal abstract Task OnCloseAsync(CancellationToken token);
        // OpenAsync doesn't take a CancellationToken so OnOpenAsync doesn't either
        internal abstract Task OnOpenAsync();
        internal virtual void OnAbort() { }

        internal async Task OpenAsync(TransportChannelListener channelListener)
        {
            Register(channelListener);
            try
            {
                using (await ThisLock.TakeLockAsync())
                {
                    if (openCount == 0)
                    {
                        await OnOpenAsync();
                    }

                    openCount++;
                }
            }
            catch
            {
                Unregister(channelListener);
                throw;
            }
        }

        internal void Abort(TransportChannelListener channelListener)
        {
            CleanupAsync(channelListener, true, CancellationToken.None);
        }

        internal abstract void Register(TransportChannelListener channelListener);

        // should only call this under ThisLock (unless accessing purely for inspection)
        protected void ThrowIfOpen()
        {
            if (openCount > 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new InvalidOperationException(SR.TransportManagerOpen));
            }
        }

        internal abstract void Unregister(TransportChannelListener channelListener);
    }


    delegate IList<TransportManager> SelectTransportManagersCallback();
    class TransportManagerContainer
    {
        IList<TransportManager> transportManagers;
        TransportChannelListener listener;
        bool closed;
        AsyncLock tableLock;

        public TransportManagerContainer(TransportChannelListener listener)
        {
            this.listener = listener;
            tableLock = listener.TransportManagerTable.AsyncLock;
            transportManagers = new List<TransportManager>();
        }

        TransportManagerContainer(TransportManagerContainer source)
        {
            listener = source.listener;
            tableLock = source.tableLock;
            transportManagers = new List<TransportManager>();
            for (int i = 0; i < source.transportManagers.Count; i++)
            {
                transportManagers.Add(source.transportManagers[i]);
            }
        }

        // copy contents into a new container (used for listener/channel lifetime decoupling)
        public static TransportManagerContainer TransferTransportManagers(TransportManagerContainer source)
        {
            TransportManagerContainer result = null;

            using (source.tableLock.TakeLock())
            {
                if (source.transportManagers.Count > 0)
                {
                    result = new TransportManagerContainer(source);
                    source.transportManagers.Clear();
                }
            }

            return result;
        }

        public void Abort()
        {
            CloseAsync(true, CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task OpenAsync(SelectTransportManagersCallback selectTransportManagerCallback)
        {
            using (await tableLock.TakeLockAsync())
            {
                if (closed) // if we've been aborted then don't get transport managers
                {
                    return;
                }

                IList<TransportManager> foundTransportManagers = selectTransportManagerCallback();
                if (foundTransportManagers == null) // nothing to do
                {
                    return;
                }

                for (int i = 0; i < foundTransportManagers.Count; i++)
                {
                    TransportManager transportManager = foundTransportManagers[i];
                    await transportManager.OpenAsync(listener);
                    transportManagers.Add(transportManager);
                }
            }
        }

        public Task CloseAsync(CancellationToken token)
        {
            return CloseAsync(false, token);
        }

        public async Task CloseAsync(bool aborting, CancellationToken token)
        {
            if (closed)
            {
                return;
            }

            IList<TransportManager> transportManagersCopy;
            using (await tableLock.TakeLockAsync())
            {
                if (closed)
                {
                    return;
                }

                closed = true;

                transportManagersCopy = new List<TransportManager>(transportManagers);
                transportManagers.Clear();

                TimeoutException timeoutException = null;
                foreach (TransportManager transportManager in transportManagersCopy)
                {
                    try
                    {
                        if (!aborting && timeoutException == null)
                        {
                            await transportManager.CloseAsync(listener, token);
                        }
                        else
                        {
                            transportManager.Abort(listener);
                        }
                    }
                    catch (TimeoutException ex)
                    {
                        timeoutException = ex;
                        transportManager.Abort(listener);
                    }
                }

                if (timeoutException != null)
                {
                    // TODO: Find a way to propagate the timeout value
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(SR.Format(SR.TimeoutOnClose, TimeSpan.Zero), timeoutException));
                }
            }
        }
    }
}
