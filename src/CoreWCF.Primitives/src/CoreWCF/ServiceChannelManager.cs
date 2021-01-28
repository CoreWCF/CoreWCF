// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF
{
    internal delegate void InstanceContextEmptyCallback(InstanceContext instanceContext);

    internal class ServiceChannelManager : LifetimeManager
    {
        private ICommunicationWaiter _activityWaiter;
        private int _activityWaiterCount;
        private readonly Action<InstanceContext> _emptyCallback;
        private IChannel _firstIncomingChannel;
        private ChannelCollection _incomingChannels;
        private ChannelCollection _outgoingChannels;
        private readonly InstanceContext _instanceContext;

        public ServiceChannelManager(InstanceContext instanceContext)
            : this(instanceContext, null)
        {
        }

        public ServiceChannelManager(InstanceContext instanceContext, Action<InstanceContext> emptyCallback)
            : base(instanceContext.ThisLock)
        {
            _instanceContext = instanceContext;
            _emptyCallback = emptyCallback;
        }

        public int ActivityCount { get; private set; }

        public ICollection<IChannel> IncomingChannels
        {
            get
            {
                EnsureIncomingChannelCollection();
                return (ICollection<IChannel>)_incomingChannels;
            }
        }

        public ICollection<IChannel> OutgoingChannels
        {
            get
            {
                if (_outgoingChannels == null)
                {
                    lock (ThisLock)
                    {
                        if (_outgoingChannels == null)
                        {
                            _outgoingChannels = new ChannelCollection(this, ThisLock);
                        }
                    }
                }
                return _outgoingChannels;
            }
        }

        public bool IsBusy
        {
            get
            {
                if (ActivityCount > 0)
                {
                    return true;
                }

                if (base.BusyCount > 0)
                {
                    return true;
                }

                ICollection<IChannel> outgoing = _outgoingChannels;
                if ((outgoing != null) && (outgoing.Count > 0))
                {
                    return true;
                }

                return false;
            }
        }

        public void AddIncomingChannel(IChannel channel)
        {
            bool added = false;

            lock (ThisLock)
            {
                if (State == LifetimeState.Opened)
                {
                    if (_firstIncomingChannel == null)
                    {
                        if (_incomingChannels == null)
                        {
                            _firstIncomingChannel = channel;
                            ChannelAdded(channel);
                        }
                        else
                        {
                            if (_incomingChannels.Contains(channel))
                            {
                                return;
                            }

                            _incomingChannels.Add(channel);
                        }
                    }
                    else
                    {
                        EnsureIncomingChannelCollection();
                        if (_incomingChannels.Contains(channel))
                        {
                            return;
                        }

                        _incomingChannels.Add(channel);
                    }
                    added = true;
                }
            }

            if (!added)
            {
                channel.Abort();
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().ToString()));
            }
        }

        private void ChannelAdded(IChannel channel)
        {
            base.IncrementBusyCount();
            channel.Closed += OnChannelClosed;
        }

        private void ChannelRemoved(IChannel channel)
        {
            channel.Closed -= OnChannelClosed;
            base.DecrementBusyCount();
        }

        public async Task CloseInputAsync(CancellationToken token)
        {
            AsyncCommunicationWaiter activityWaiter = null;

            lock (ThisLock)
            {
                if (ActivityCount > 0)
                {
                    activityWaiter = new AsyncCommunicationWaiter(ThisLock);
                    if (!(_activityWaiter == null))
                    {
                        Fx.Assert("ServiceChannelManager.CloseInput: (this.activityWaiter == null)");
                    }
                    _activityWaiter = activityWaiter;
                    Interlocked.Increment(ref _activityWaiterCount);
                }
            }

            if (activityWaiter != null)
            {
                CommunicationWaitResult result = await activityWaiter.WaitAsync(false, token);
                if (Interlocked.Decrement(ref _activityWaiterCount) == 0)
                {
                    activityWaiter.Dispose();
                    _activityWaiter = null;
                }

                switch (result)
                {
                    case CommunicationWaitResult.Expired:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new TimeoutException(SR.SfxCloseTimedOutWaitingForDispatchToComplete));
                    case CommunicationWaitResult.Aborted:
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().ToString()));
                }
            }
        }

        public void DecrementActivityCount()
        {
            ICommunicationWaiter activityWaiter = null;
            bool empty = false;

            lock (ThisLock)
            {
                if (!(ActivityCount > 0))
                {
                    Fx.Assert("ServiceChannelManager.DecrementActivityCount: (this.activityCount > 0)");
                }
                if (--ActivityCount == 0)
                {
                    if (_activityWaiter != null)
                    {
                        activityWaiter = _activityWaiter;
                        Interlocked.Increment(ref _activityWaiterCount);
                    }
                    if (BusyCount == 0)
                    {
                        empty = true;
                    }
                }
            }

            if (activityWaiter != null)
            {
                activityWaiter.Signal();
                if (Interlocked.Decrement(ref _activityWaiterCount) == 0)
                {
                    activityWaiter.Dispose();
                    _activityWaiter = null;
                }
            }

            if (empty && State == LifetimeState.Opened)
            {
                OnEmpty();
            }
        }

        private void EnsureIncomingChannelCollection()
        {
            lock (ThisLock)
            {
                if (_incomingChannels == null)
                {
                    _incomingChannels = new ChannelCollection(this, ThisLock);
                    if (_firstIncomingChannel != null)
                    {
                        _incomingChannels.Add(_firstIncomingChannel);
                        ChannelRemoved(_firstIncomingChannel); // Adding to collection called ChannelAdded, so call ChannelRemoved to balance
                        _firstIncomingChannel = null;
                    }
                }
            }
        }

        public void IncrementActivityCount()
        {
            lock (ThisLock)
            {
                if (State == LifetimeState.Closed)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().ToString()));
                }

                ActivityCount++;
            }
        }

        protected override void IncrementBusyCount()
        {
            base.IncrementBusyCount();
        }

        protected override void OnAbort()
        {
            IChannel[] channels = SnapshotChannels();
            for (int index = 0; index < channels.Length; index++)
            {
                channels[index].Abort();
            }

            ICommunicationWaiter activityWaiter = null;

            lock (ThisLock)
            {
                if (_activityWaiter != null)
                {
                    activityWaiter = _activityWaiter;
                    Interlocked.Increment(ref _activityWaiterCount);
                }
            }

            if (activityWaiter != null)
            {
                activityWaiter.Signal();
                if (Interlocked.Decrement(ref _activityWaiterCount) == 0)
                {
                    activityWaiter.Dispose();
                    _activityWaiter = null;
                }
            }

            base.OnAbort();
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            await CloseInputAsync(token);
            await base.OnCloseAsync(token);
        }

        protected override void OnEmpty()
        {
            if (_emptyCallback != null)
            {
                _emptyCallback(_instanceContext);
            }
        }

        private void OnChannelClosed(object sender, EventArgs args)
        {
            RemoveChannel((IChannel)sender);
        }

        public bool RemoveChannel(IChannel channel)
        {
            lock (ThisLock)
            {
                if (_firstIncomingChannel == channel)
                {
                    _firstIncomingChannel = null;
                    ChannelRemoved(channel);
                    return true;
                }
                else if (_incomingChannels != null && _incomingChannels.Contains(channel))
                {
                    _incomingChannels.Remove(channel);
                    return true;
                }
                else if (_outgoingChannels != null && _outgoingChannels.Contains(channel))
                {
                    _outgoingChannels.Remove(channel);
                    return true;
                }
            }

            return false;
        }

        public IChannel[] SnapshotChannels()
        {
            lock (ThisLock)
            {
                int outgoingCount = (_outgoingChannels != null ? _outgoingChannels.Count : 0);

                if (_firstIncomingChannel != null)
                {
                    IChannel[] channels = new IChannel[1 + outgoingCount];
                    channels[0] = _firstIncomingChannel;
                    if (outgoingCount > 0)
                    {
                        _outgoingChannels.CopyTo(channels, 1);
                    }

                    return channels;
                }

                if (_incomingChannels != null)
                {
                    IChannel[] channels = new IChannel[_incomingChannels.Count + outgoingCount];
                    _incomingChannels.CopyTo(channels, 0);
                    if (outgoingCount > 0)
                    {
                        _outgoingChannels.CopyTo(channels, _incomingChannels.Count);
                    }

                    return channels;
                }

                if (outgoingCount > 0)
                {
                    IChannel[] channels = new IChannel[outgoingCount];
                    _outgoingChannels.CopyTo(channels, 0);
                    return channels;
                }
            }
            return EmptyArray<IChannel>.Allocate(0);
        }

        private class ChannelCollection : ICollection<IChannel>
        {
            private readonly ServiceChannelManager _channelManager;
            private readonly object _syncRoot;
            private readonly HashSet<IChannel> _hashSet = new HashSet<IChannel>();

            public bool IsReadOnly
            {
                get { return false; }
            }

            public int Count
            {
                get
                {
                    lock (_syncRoot)
                    {
                        return _hashSet.Count;
                    }
                }
            }

            public ChannelCollection(ServiceChannelManager channelManager, object syncRoot)
            {
                if (syncRoot == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(syncRoot));
                }

                _channelManager = channelManager;
                _syncRoot = syncRoot;
            }

            public void Add(IChannel channel)
            {
                lock (_syncRoot)
                {
                    if (_hashSet.Add(channel))
                    {
                        _channelManager.ChannelAdded(channel);
                    }
                }
            }

            public void Clear()
            {
                lock (_syncRoot)
                {
                    foreach (IChannel channel in _hashSet)
                    {
                        _channelManager.ChannelRemoved(channel);
                    }

                    _hashSet.Clear();
                }
            }

            public bool Contains(IChannel channel)
            {
                lock (_syncRoot)
                {
                    if (channel != null)
                    {
                        return _hashSet.Contains(channel);
                    }
                    return false;
                }
            }

            public void CopyTo(IChannel[] array, int arrayIndex)
            {
                lock (_syncRoot)
                {
                    _hashSet.CopyTo(array, arrayIndex);
                }
            }

            public bool Remove(IChannel channel)
            {
                lock (_syncRoot)
                {
                    bool ret = false;
                    if (channel != null)
                    {
                        ret = _hashSet.Remove(channel);
                        if (ret)
                        {
                            _channelManager.ChannelRemoved(channel);
                        }
                    }
                    return ret;
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                lock (_syncRoot)
                {
                    return _hashSet.GetEnumerator();
                }
            }

            IEnumerator<IChannel> IEnumerable<IChannel>.GetEnumerator()
            {
                lock (_syncRoot)
                {
                    return _hashSet.GetEnumerator();
                }
            }
        }
    }
}