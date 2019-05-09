using System;
using System.Threading;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    class TerminatingOperationBehavior
    {
        static void AbortChannel(object state)
        {
            ((IChannel)state).Abort();
        }

        public static TerminatingOperationBehavior CreateIfNecessary(DispatchRuntime dispatch)
        {
            if (IsTerminatingOperationBehaviorNeeded(dispatch))
            {
                return new TerminatingOperationBehavior();
            }
            else
            {
                return null;
            }
        }

        static bool IsTerminatingOperationBehaviorNeeded(DispatchRuntime dispatch)
        {
            for (int i = 0; i < dispatch.Operations.Count; i++)
            {
                DispatchOperation operation = dispatch.Operations[i];

                if (operation.IsTerminating)
                {
                    return true;
                }
            }

            return false;
        }

        internal void AfterReply(ref MessageRpc rpc)
        {
            if (rpc.Operation.IsTerminating && rpc.Channel.HasSession)
            {
                Timer timer = new Timer(new TimerCallback(TerminatingOperationBehavior.AbortChannel), rpc.Channel.Binder.Channel, rpc.Channel.CloseTimeout, TimeSpan.FromMilliseconds(-1));
            }
        }

        internal static void AfterReply(ref ProxyRpc rpc)
        {
            if (rpc.Operation.IsTerminating && rpc.Channel.HasSession)
            {
                IChannel sessionChannel = rpc.Channel.Binder.Channel;
                rpc.Channel.CloseAsync(rpc.CancellationToken).GetAwaiter().GetResult();
            }
        }
    }

}