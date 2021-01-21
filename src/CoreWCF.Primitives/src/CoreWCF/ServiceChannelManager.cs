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
    delegate void InstanceContextEmptyCallback(InstanceContext instanceContext);

    internal class ServiceChannelManager : LifetimeManager
    {
        int activityCount;
        ICommunicationWaiter activityWaiter;
        int activityWaiterCount;
        Action<InstanceContext> emptyCallback;
        IChannel firstIncomingChannel;
        ChannelCollection incomingChannels;
        ChannelCollection outgoingChannels;
        InstanceContext instanceContext;

        public ServiceChannelManager(InstanceContext instanceContext)
            : this(instanceContext, null)
        {
        }

        public ServiceChannelManager(InstanceContext instanceContext, Action<InstanceContext> emptyCallback)
            : base(instanceContext.ThisLock)
        {
            this.instanceContext = instanceContext;
            this.emptyCallback = emptyCallback;
        }

        public int ActivityCount
        {
            get { return activityCount; }
        }

        public ICollection<IChannel> IncomingChannels
        {
            get
            {
                EnsureIncomingChannelCollection();
                return (ICollection<IChannel>)incomingChannels;
            }
        }

        public ICollection<IChannel> OutgoingChannels
        {
            get
            {
                if (outgoingChannels == null)
                {
                    lock (ThisLock)
                    {
                        if (outgoingChannels == null)
                            outgoingChannels = new ChannelCollection(this, ThisLock);
                    }
                }
                return outgoingChannels;
            }
        }

        public bool IsBusy
        {
            get
            {
                if (ActivityCount > 0)
                    return true;

                if (base.BusyCount > 0)
                    return true;

                ICollection<IChannel> outgoing = outgoingChannels;
                if ((outgoing != null) && (outgoing.Count > 0))
                    return true;

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
                    if (firstIncomingChannel == null)
                    {
                        if (incomingChannels == null)
                        {
                            firstIncomingChannel = channel;
                            ChannelAdded(channel);
                        }
                        else
                        {
                            if (incomingChannels.Contains(channel))
                                return;
                            incomingChannels.Add(channel);
                        }
                    }
                    else
                    {
                        EnsureIncomingChannelCollection();
                        if (incomingChannels.Contains(channel))
                            return;
                        incomingChannels.Add(channel);
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

        void ChannelAdded(IChannel channel)
        {
            base.IncrementBusyCount();
            channel.Closed += OnChannelClosed;
        }

        void ChannelRemoved(IChannel channel)
        {
            channel.Closed -= OnChannelClosed;
            base.DecrementBusyCount();
        }

        public async Task CloseInputAsync(CancellationToken token)
        {
            AsyncCommunicationWaiter activityWaiter = null;

            lock (ThisLock)
            {
                if (activityCount > 0)
                {
                    activityWaiter = new AsyncCommunicationWaiter(ThisLock);
                    if (!(this.activityWaiter == null))
                    {
                        Fx.Assert("ServiceChannelManager.CloseInput: (this.activityWaiter == null)");
                    }
                    this.activityWaiter = activityWaiter;
                    Interlocked.Increment(ref activityWaiterCount);
                }
            }

            if (activityWaiter != null)
            {
                CommunicationWaitResult result = await activityWaiter.WaitAsync(false, token);
                if (Interlocked.Decrement(ref activityWaiterCount) == 0)
                {
                    activityWaiter.Dispose();
                    this.activityWaiter = null;
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
                if (!(activityCount > 0))
                {
                    Fx.Assert("ServiceChannelManager.DecrementActivityCount: (this.activityCount > 0)");
                }
                if (--activityCount == 0)
                {
                    if (this.activityWaiter != null)
                    {
                        activityWaiter = this.activityWaiter;
                        Interlocked.Increment(ref activityWaiterCount);
                    }
                    if (BusyCount == 0)
                        empty = true;
                }
            }

            if (activityWaiter != null)
            {
                activityWaiter.Signal();
                if (Interlocked.Decrement(ref activityWaiterCount) == 0)
                {
                    activityWaiter.Dispose();
                    this.activityWaiter = null;
                }
            }

            if (empty && State == LifetimeState.Opened)
                OnEmpty();
        }

        void EnsureIncomingChannelCollection()
        {
            lock (ThisLock)
            {
                if (incomingChannels == null)
                {
                    incomingChannels = new ChannelCollection(this, ThisLock);
                    if (firstIncomingChannel != null)
                    {
                        incomingChannels.Add(firstIncomingChannel);
                        ChannelRemoved(firstIncomingChannel); // Adding to collection called ChannelAdded, so call ChannelRemoved to balance
                        firstIncomingChannel = null;
                    }
                }
            }
        }

        public void IncrementActivityCount()
        {
            lock (ThisLock)
            {
                if (State == LifetimeState.Closed)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().ToString()));
                activityCount++;
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
                channels[index].Abort();

            ICommunicationWaiter activityWaiter = null;

            lock (ThisLock)
            {
                if (this.activityWaiter != null)
                {
                    activityWaiter = this.activityWaiter;
                    Interlocked.Increment(ref activityWaiterCount);
                }
            }

            if (activityWaiter != null)
            {
                activityWaiter.Signal();
                if (Interlocked.Decrement(ref activityWaiterCount) == 0)
                {
                    activityWaiter.Dispose();
                    this.activityWaiter = null;
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
            if (emptyCallback != null)
                emptyCallback(instanceContext);
        }

        void OnChannelClosed(object sender, EventArgs args)
        {
            RemoveChannel((IChannel)sender);
        }

        public bool RemoveChannel(IChannel channel)
        {
            lock (ThisLock)
            {
                if (firstIncomingChannel == channel)
                {
                    firstIncomingChannel = null;
                    ChannelRemoved(channel);
                    return true;
                }
                else if (incomingChannels != null && incomingChannels.Contains(channel))
                {
                    incomingChannels.Remove(channel);
                    return true;
                }
                else if (outgoingChannels != null && outgoingChannels.Contains(channel))
                {
                    outgoingChannels.Remove(channel);
                    return true;
                }
            }

            return false;
        }

        public IChannel[] SnapshotChannels()
        {
            lock (ThisLock)
            {
                int outgoingCount = (outgoingChannels != null ? outgoingChannels.Count : 0);

                if (firstIncomingChannel != null)
                {
                    IChannel[] channels = new IChannel[1 + outgoingCount];
                    channels[0] = firstIncomingChannel;
                    if (outgoingCount > 0)
                        outgoingChannels.CopyTo(channels, 1);
                    return channels;
                }

                if (incomingChannels != null)
                {
                    IChannel[] channels = new IChannel[incomingChannels.Count + outgoingCount];
                    incomingChannels.CopyTo(channels, 0);
                    if (outgoingCount > 0)
                        outgoingChannels.CopyTo(channels, incomingChannels.Count);
                    return channels;
                }

                if (outgoingCount > 0)
                {
                    IChannel[] channels = new IChannel[outgoingCount];
                    outgoingChannels.CopyTo(channels, 0);
                    return channels;
                }
            }
            return EmptyArray<IChannel>.Allocate(0);
        }

        class ChannelCollection : ICollection<IChannel>
        {
            ServiceChannelManager channelManager;
            object syncRoot;
            HashSet<IChannel> hashSet = new HashSet<IChannel>();

            public bool IsReadOnly
            {
                get { return false; }
            }

            public int Count
            {
                get
                {
                    lock (syncRoot)
                    {
                        return hashSet.Count;
                    }
                }
            }

            public ChannelCollection(ServiceChannelManager channelManager, object syncRoot)
            {
                if (syncRoot == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(syncRoot));

                this.channelManager = channelManager;
                this.syncRoot = syncRoot;
            }

            public void Add(IChannel channel)
            {
                lock (syncRoot)
                {
                    if (hashSet.Add(channel))
                    {
                        channelManager.ChannelAdded(channel);
                    }
                }
            }

            public void Clear()
            {
                lock (syncRoot)
                {
                    foreach (IChannel channel in hashSet)
                        channelManager.ChannelRemoved(channel);
                    hashSet.Clear();
                }
            }

            public bool Contains(IChannel channel)
            {
                lock (syncRoot)
                {
                    if (channel != null)
                    {
                        return hashSet.Contains(channel);
                    }
                    return false;
                }
            }

            public void CopyTo(IChannel[] array, int arrayIndex)
            {
                lock (syncRoot)
                {
                    hashSet.CopyTo(array, arrayIndex);
                }
            }

            public bool Remove(IChannel channel)
            {
                lock (syncRoot)
                {
                    bool ret = false;
                    if (channel != null)
                    {
                        ret = hashSet.Remove(channel);
                        if (ret)
                        {
                            channelManager.ChannelRemoved(channel);
                        }
                    }
                    return ret;
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                lock (syncRoot)
                {
                    return hashSet.GetEnumerator();
                }
            }

            IEnumerator<IChannel> IEnumerable<IChannel>.GetEnumerator()
            {
                lock (syncRoot)
                {
                    return hashSet.GetEnumerator();
                }
            }
        }
    }

}