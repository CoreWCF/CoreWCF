using System;
using System.Threading;

namespace Microsoft.Runtime
{
    internal class ServiceModelSynchronizationContext : SynchronizationContext
    {
        public static ServiceModelSynchronizationContext Instance = new ServiceModelSynchronizationContext();

        public override void Post(SendOrPostCallback d, object state)
        {
            IOThreadScheduler.ScheduleCallbackNoFlow(
                (s) => { d(s); }, state);
        }
    }
}