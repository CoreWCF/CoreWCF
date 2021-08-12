// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    internal class ReplyChannel : ServiceChannelBase, IReplyChannel
    {
        public ReplyChannel(IDefaultCommunicationTimeouts timeouts, EndpointAddress localAddress) : base(timeouts)
        {
            LocalAddress = localAddress;
        }

        public EndpointAddress LocalAddress { get; }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IReplyChannel))
            {
                return (T)(object)this;
            }

            T baseProperty = base.GetProperty<T>();
            if (baseProperty != null)
            {
                return baseProperty;
            }

            return default;
        }

        protected override void OnAbort() { }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}