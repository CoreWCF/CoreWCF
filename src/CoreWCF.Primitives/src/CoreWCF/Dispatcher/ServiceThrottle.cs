using CoreWCF.Runtime;
using System;
using System.Threading.Tasks;

namespace CoreWCF.Dispatcher
{
    public sealed class ServiceThrottle
    {
        internal const int DefaultMaxConcurrentCalls = 16;
        internal const int DefaultMaxConcurrentSessions = 100;
        internal static int DefaultMaxConcurrentCallsCpuCount = DefaultMaxConcurrentCalls * Environment.ProcessorCount;
        internal static int DefaultMaxConcurrentSessionsCpuCount = DefaultMaxConcurrentSessions * Environment.ProcessorCount;

        private FlowThrottle _calls;
        private FlowThrottle _sessions;
        private QuotaThrottle _dynamic;
        private FlowThrottle _instanceContexts;

        private readonly ServiceHostBase _host;

        // TODO: Performance counters
        //ServicePerformanceCountersBase servicePerformanceCounters;
        private bool _isActive;
        readonly object _thisLock = new object();

        internal ServiceThrottle(ServiceHostBase host)
        {
            if (!((host != null)))
            {
                Fx.Assert("ServiceThrottle.ServiceThrottle: (host != null)");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(host));
            }
            _host = host;
            MaxConcurrentCalls = DefaultMaxConcurrentCallsCpuCount;
            MaxConcurrentSessions = DefaultMaxConcurrentSessionsCpuCount;

            _isActive = true;
        }

        FlowThrottle Calls
        {
            get
            {
                if (_calls == null)
                {
                    lock (ThisLock)
                    {
                        if (_calls == null)
                        {
                            FlowThrottle callsFt = new FlowThrottle(DefaultMaxConcurrentCallsCpuCount,
                                MaxConcurrentCallsPropertyName, MaxConcurrentCallsConfigName);

                            callsFt.SetRatio(RatioCallsToken);

                            _calls = callsFt;
                        }
                    }
                }

                return _calls;
            }
        }

        FlowThrottle Sessions
        {
            get
            {
                if (_sessions == null)
                {
                    lock (ThisLock)
                    {
                        if (_sessions == null)
                        {
                            FlowThrottle sessionsFt = new FlowThrottle(DefaultMaxConcurrentSessionsCpuCount,
                                MaxConcurrentSessionsPropertyName, MaxConcurrentSessionsConfigName);

                            sessionsFt.SetRatio(RatioSessionsToken);

                            _sessions = sessionsFt;
                        }
                    }
                }

                return _sessions;
            }
        }

        QuotaThrottle Dynamic
        {
            get
            {
                if (_dynamic == null)
                {
                    lock (ThisLock)
                    {
                        if (_dynamic == null)
                        {
                            QuotaThrottle dynamicQt = new QuotaThrottle(new object());
                            dynamicQt.Owner = "ServiceHost";

                            _dynamic = dynamicQt;
                        }
                    }
                }

                UpdateIsActive();
                return _dynamic;
            }
        }

        internal int ManualFlowControlLimit
        {
            get { return Dynamic.Limit; }
            set { Dynamic.SetLimit(value); }
        }

        const string MaxConcurrentCallsPropertyName = "MaxConcurrentCalls";
        const string MaxConcurrentCallsConfigName = "maxConcurrentCalls";
        public int MaxConcurrentCalls
        {
            get { return Calls.Capacity; }
            set
            {
                ThrowIfClosedOrOpened(MaxConcurrentCallsPropertyName);
                Calls.Capacity = value;
                UpdateIsActive();
                //if (null != this.servicePerformanceCounters)
                //{
                //    this.servicePerformanceCounters.SetThrottleBase((int)ServicePerformanceCounters.PerfCounters.CallsPercentMaxCallsBase, Calls.Capacity);
                //}
            }
        }

        const string MaxConcurrentSessionsPropertyName = "MaxConcurrentSessions";
        const string MaxConcurrentSessionsConfigName = "maxConcurrentSessions";
        public int MaxConcurrentSessions
        {
            get { return Sessions.Capacity; }
            set
            {
                ThrowIfClosedOrOpened(MaxConcurrentSessionsPropertyName);
                Sessions.Capacity = value;
                UpdateIsActive();
                //if (null != this.servicePerformanceCounters)
                //{
                //    this.servicePerformanceCounters.SetThrottleBase((int)ServicePerformanceCounters.PerfCounters.SessionsPercentMaxSessionsBase, Sessions.Capacity);
                //}
            }
        }

        const string MaxConcurrentInstancesPropertyName = "MaxConcurrentInstances";
        const string MaxConcurrentInstancesConfigName = "maxConcurrentInstances";
        public int MaxConcurrentInstances
        {
            get { return InstanceContexts.Capacity; }
            set
            {
                ThrowIfClosedOrOpened(MaxConcurrentInstancesPropertyName);
                InstanceContexts.Capacity = value;
                UpdateIsActive();
                //if (null != this.servicePerformanceCounters)
                //{
                //    this.servicePerformanceCounters.SetThrottleBase((int)ServicePerformanceCounters.PerfCounters.InstancesPercentMaxInstancesBase, InstanceContexts.Capacity);
                //}
            }
        }

        FlowThrottle InstanceContexts
        {
            get
            {
                if (_instanceContexts == null)
                {
                    lock (ThisLock)
                    {
                        if (_instanceContexts == null)
                        {
                            FlowThrottle instanceContextsFt = new FlowThrottle(Int32.MaxValue,
                                                                     MaxConcurrentInstancesPropertyName, MaxConcurrentInstancesConfigName);
                            instanceContextsFt.SetRatio(RatioInstancesToken);

                            //if (this.servicePerformanceCounters != null)
                            //{
                            //    InitializeInstancePerfCounterSettings(instanceContextsFt);
                            //}

                            _instanceContexts = instanceContextsFt;
                        }
                    }
                }

                return _instanceContexts;
            }
        }

        internal bool IsActive
        {
            get { return _isActive; }
        }

        internal object ThisLock
        {
            get { return _thisLock; }
        }

        //internal void SetServicePerformanceCounters(ServicePerformanceCountersBase counters)
        //{
        //    this.servicePerformanceCounters = counters;
        //    //instance throttle is created through the behavior, set the perf counter callbacks if initialized
        //    if (_instanceContexts != null)
        //    {
        //        InitializeInstancePerfCounterSettings(_instanceContexts);
        //    }

        //    //this.calls and this.sessions throttles are created by the constructor. Set the perf counter callbacks
        //    InitializeCallsPerfCounterSettings();
        //    InitializeSessionsPerfCounterSettings();
        //}

        //void InitializeInstancePerfCounterSettings(FlowThrottle instanceContextsFt)
        //{
        //    Fx.Assert(instanceContextsFt != null, "Expect instanceContext to be initialized");
        //    Fx.Assert(this.servicePerformanceCounters != null, "expect servicePerformanceCounters to be set");
        //    instanceContextsFt.SetAcquired(AcquiredInstancesToken);
        //    instanceContextsFt.SetReleased(ReleasedInstancesToken);
        //    instanceContextsFt.SetRatio(RatioInstancesToken);
        //    this.servicePerformanceCounters.SetThrottleBase((int)ServicePerformanceCounters.PerfCounters.InstancesPercentMaxInstancesBase, instanceContextsFt.Capacity);
        //}

        //void InitializeCallsPerfCounterSettings()
        //{
        //    Fx.Assert(_calls != null, "Expect calls to be initialized");
        //    Fx.Assert(this.servicePerformanceCounters != null, "expect servicePerformanceCounters to be set");
        //    _calls.SetAcquired(AcquiredCallsToken);
        //    _calls.SetReleased(ReleasedCallsToken);
        //    _calls.SetRatio(RatioCallsToken);
        //    this.servicePerformanceCounters.SetThrottleBase((int)ServicePerformanceCounters.PerfCounters.CallsPercentMaxCallsBase, _calls.Capacity);
        //}

        //void InitializeSessionsPerfCounterSettings()
        //{
        //    Fx.Assert(_sessions != null, "Expect sessions to be initialized");
        //    Fx.Assert(this.servicePerformanceCounters != null, "expect servicePerformanceCounters to be set");
        //    _sessions.SetAcquired(AcquiredSessionsToken);
        //    _sessions.SetReleased(ReleasedSessionsToken);
        //    _sessions.SetRatio(RatioSessionsToken);
        //    this.servicePerformanceCounters.SetThrottleBase((int)ServicePerformanceCounters.PerfCounters.SessionsPercentMaxSessionsBase, _sessions.Capacity);
        //}

        private async ValueTask PrivateAcquireCallAsync()
        {
            if (_calls != null)
            {
                await _calls.AcquireAsync();
            }
        }

        //bool PrivateAcquireSessionListenerHandler(ListenerHandler listener)
        //{
        //    if ((_sessions != null) && (listener.Channel != null) && (listener.Channel.Throttle == null))
        //    {
        //        listener.Channel.Throttle = this;
        //        return _sessions.Acquire(listener);
        //    }
        //    else
        //    {
        //        return true;
        //    }
        //}

        private async ValueTask PrivateAcquireSessionAsync()
        {
            if (_sessions != null)
            {
                await _sessions.AcquireAsync();
            }
        }

        private async ValueTask PrivateAcquireDynamicAsync()
        {
            if (_dynamic != null)
            {
                await _dynamic.AcquireAsync();
            }
        }

        private async ValueTask PrivateAcquireInstanceContextAsync(ChannelHandler channel)
        {
            if ((_instanceContexts != null) && (channel.InstanceContext == null))
            {
                channel.InstanceContextServiceThrottle = this;
                await _instanceContexts.AcquireAsync();
            }
        }

        internal ValueTask AcquireCall()
        {
            return PrivateAcquireCallAsync();
        }

        internal async ValueTask AcquireInstanceContextAndDynamicAsync(ChannelHandler channel, bool acquireInstanceContextThrottle)
        {
            // TODO: Lock removed. This code looks like it should be safe to execute without the lock. Need to verify.
            if (acquireInstanceContextThrottle)
            {
                await PrivateAcquireInstanceContextAsync(channel);
            }

            await PrivateAcquireDynamicAsync();
        }

        internal ValueTask AcquireSessionAsync()
        {
            return PrivateAcquireSessionAsync();
        }

        internal void DeactivateChannel()
        {
            if (_isActive)
            {
                if (_sessions != null)
                    _sessions.Release();
            }
        }

        internal void DeactivateCall()
        {
            if (_isActive)
            {
                if (_calls != null)
                    _calls.Release();
            }
        }

        internal void DeactivateInstanceContext()
        {
            if (_isActive)
            {
                if (_instanceContexts != null)
                {
                    _instanceContexts.Release();
                }
            }
        }

        internal int IncrementManualFlowControlLimit(int incrementBy)
        {
            return Dynamic.IncrementLimit(incrementBy);
        }

        void ThrowIfClosedOrOpened(string memberName)
        {
            if (_host.State == CommunicationState.Opened)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.SFxImmutableThrottle1, memberName)));
            }
            else
            {
                _host.ThrowIfClosedOrOpened();
            }
        }

        void UpdateIsActive()
        {
            _isActive = ((_dynamic != null) ||
                             ((_calls != null) && (_calls.Capacity != Int32.MaxValue)) ||
                             ((_sessions != null) && (_sessions.Capacity != Int32.MaxValue)) ||
                             ((_instanceContexts != null) && (_instanceContexts.Capacity != Int32.MaxValue)));
        }

        //internal void AcquiredCallsToken()
        //{
        //    this.servicePerformanceCounters.IncrementThrottlePercent((int)ServicePerformanceCounters.PerfCounters.CallsPercentMaxCalls);
        //}

        //internal void ReleasedCallsToken()
        //{
        //    this.servicePerformanceCounters.DecrementThrottlePercent((int)ServicePerformanceCounters.PerfCounters.CallsPercentMaxCalls);
        //}

        internal void RatioCallsToken(int count)
        {
            //if (TD.ConcurrentCallsRatioIsEnabled())
            //{
            //    TD.ConcurrentCallsRatio(count, MaxConcurrentCalls);
            //}
        }

        //internal void AcquiredInstancesToken()
        //{
        //    this.servicePerformanceCounters.IncrementThrottlePercent((int)ServicePerformanceCounters.PerfCounters.InstancesPercentMaxInstances);
        //}

        //internal void ReleasedInstancesToken()
        //{
        //    this.servicePerformanceCounters.DecrementThrottlePercent((int)ServicePerformanceCounters.PerfCounters.InstancesPercentMaxInstances);
        //}

        internal void RatioInstancesToken(int count)
        {
            //if (TD.ConcurrentInstancesRatioIsEnabled())
            //{
            //    TD.ConcurrentInstancesRatio(count, MaxConcurrentInstances);
            //}
        }

        //internal void AcquiredSessionsToken()
        //{
        //    this.servicePerformanceCounters.IncrementThrottlePercent((int)ServicePerformanceCounters.PerfCounters.SessionsPercentMaxSessions);
        //}

        //internal void ReleasedSessionsToken()
        //{
        //    this.servicePerformanceCounters.DecrementThrottlePercent((int)ServicePerformanceCounters.PerfCounters.SessionsPercentMaxSessions);
        //}

        internal void RatioSessionsToken(int count)
        {
            //if (TD.ConcurrentSessionsRatioIsEnabled())
            //{
            //    TD.ConcurrentSessionsRatio(count, MaxConcurrentSessions);
            //}
        }
    }
}