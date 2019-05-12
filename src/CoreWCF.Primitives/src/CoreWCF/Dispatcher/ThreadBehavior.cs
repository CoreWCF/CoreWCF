using System;
using System.Threading;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class ThreadBehavior
    {
        readonly SynchronizationContext context;

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
            //if (AspNetEnvironment.IsApplicationDomainHosted())
            //{
            //    return null;
            //}
            return SynchronizationContext.Current;
        }
    }

}