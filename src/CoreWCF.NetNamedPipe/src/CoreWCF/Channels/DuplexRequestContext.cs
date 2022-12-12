// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class DuplexRequestContext : RequestContextBase
    {
        private readonly IDuplexChannel _channel;
        private TaskCompletionSource<object> _dispatchInvokedTcs;
        private Task _dispatchInvokedTask;

        internal DuplexRequestContext(IDuplexChannel channel, Message request, IDefaultCommunicationTimeouts timeouts)
            : base(request, timeouts.CloseTimeout, timeouts.SendTimeout)
        {
            _channel = channel;
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
                return _channel.SendAsync(message, token);
            }

            return Task.CompletedTask;
        }

        public override void OnOperationInvoke()
        {
            if (_dispatchInvokedTask == null)
            {
                if (Interlocked.CompareExchange(ref _dispatchInvokedTask, Task.CompletedTask, null) == null)
                {
                    return;
                }
            }

            Fx.Assert(_dispatchInvokedTcs != null, "A non-null Task should have the associated TCS set");
            _dispatchInvokedTcs.TrySetResult(null);
        }

        // TODO: Switch to ValueTask as in many scenarios this will be completed before being requested;
        public Task OperationDispatching
        {
            get
            {
                if (_dispatchInvokedTask != null)
                {
                    return _dispatchInvokedTask;
                }

                // There's a small race here where we could create a TCS and dispatchInvokedTask is set via OnOperationInvoke.
                // In this case, we won't use our TCS and it will simply be an unnecessary allocation. As dispatchInvokedTask is
                // always set using Interlocked.CompareExchance, we guarantee returning the correct Task;
                // Creating with RunContinuationsAsynchronously otherwise the method awaiting the Task will continue executing on
                // the thread which calls OnOperationInvoke;
                _dispatchInvokedTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (Interlocked.CompareExchange(ref _dispatchInvokedTask, _dispatchInvokedTcs.Task, null) == null)
                {
                    return _dispatchInvokedTcs.Task;
                }
                else
                {
                    _dispatchInvokedTcs = null;
                    return _dispatchInvokedTask;
                }
            }
        }
    }
}
