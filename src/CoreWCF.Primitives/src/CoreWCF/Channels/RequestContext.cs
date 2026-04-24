// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    public abstract class RequestContext : IDisposable
    {
        public abstract Message RequestMessage { get; }
        public abstract void Abort();
        public abstract Task ReplyAsync(Message message);
        public abstract Task ReplyAsync(Message message, CancellationToken token);
        public abstract Task CloseAsync();
        public abstract Task CloseAsync(CancellationToken token);
        protected virtual void Dispose(bool disposing) { }
        public virtual void OnOperationInvoke() { }
        void IDisposable.Dispose() => Dispose(true);
    }
}
