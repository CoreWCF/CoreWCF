// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Runtime
{
    internal class ServiceModelSynchronizationContext : SynchronizationContext
    {
        public static ServiceModelSynchronizationContext Instance = new ServiceModelSynchronizationContext();

        public override void Post(SendOrPostCallback d, object state)
        {
            Task.Factory.StartNew((s) => d(s), state, default, TaskCreationOptions.RunContinuationsAsynchronously, IOThreadScheduler.IOTaskScheduler);
        }
    }
}
