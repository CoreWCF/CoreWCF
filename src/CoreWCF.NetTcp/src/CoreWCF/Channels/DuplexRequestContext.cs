// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    class DuplexRequestContext : RequestContextBase
    {
        IDuplexChannel channel;
        TaskCompletionSource<object> dispatchInvokedTcs;
        Task dispatchInvokedTask;

        internal DuplexRequestContext(IDuplexChannel channel, Message request, IDefaultCommunicationTimeouts timeouts)
            : base(request, timeouts.CloseTimeout, timeouts.SendTimeout)
        {
            this.channel = channel;
        }

        protected override void OnAbort()
        {
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override Task OnReplyAsync(Message message, CancellationToken token)
        {
            if (message != null)
            {
                return channel.SendAsync(message, token);
            }

            return Task.CompletedTask;
        }

        public override void OnOperationInvoke()
        {
            if (dispatchInvokedTask == null)
            {
                if (Interlocked.CompareExchange(ref dispatchInvokedTask, Task.CompletedTask, null) == null)
                {
                    return;
                }
            }

            Fx.Assert(dispatchInvokedTcs != null, "A non-null Task should have the associated TCS set");
            dispatchInvokedTcs.TrySetResult(null);
        }

        // TODO: Switch to ValueTask as in many scenarios this will be completed before being requested;
        public Task OperationDispatching
        {
            get
            {
                if (dispatchInvokedTask != null)
                {
                    return dispatchInvokedTask;
                }

                // There's a small race here where we could create a TCS and dispatchInvokedTask is set via OnOperationInvoke.
                // In this case, we won't use our TCS and it will simply be an unnecessary allocation. As dispatchInvokedTask is
                // always set using Interlocked.CompareExchance, we guarantee returning the correct Task;
                // Creating with RunContinuationsAsynchronously otherwise the method awaiting the Task will continue executing on
                // the thread which calls OnOperationInvoke;
                dispatchInvokedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (Interlocked.CompareExchange(ref dispatchInvokedTask, dispatchInvokedTcs.Task, null) == null)
                {
                    return dispatchInvokedTcs.Task;
                }
                else
                {
                    dispatchInvokedTcs = null;
                    return dispatchInvokedTask;
                }
            }
        }
    }
}
