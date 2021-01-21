// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class ThreadBehavior
    {
        private readonly SynchronizationContext context;

        internal ThreadBehavior(DispatchRuntime dispatch)
        {
            context = dispatch.SynchronizationContext;
        }

        internal SynchronizationContext GetSyncContext(MessageRpc rpc)
        {
            Fx.Assert(rpc.InstanceContext != null, "instanceContext is null !");
            SynchronizationContext syncContext = rpc.InstanceContext.SynchronizationContext ?? context;
            return syncContext;
        }

        internal static SynchronizationContext GetCurrentSynchronizationContext()
        {
            return SynchronizationContext.Current;
        }
    }

}