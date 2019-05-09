using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal class MultipleReceiveBinder : IChannelBinder
    {
        internal static class MultipleReceiveDefaults
        {
            internal const int MaxPendingReceives = 1;
        }

        IChannelBinder channelBinder;
        bool ordered;

        public MultipleReceiveBinder(IChannelBinder channelBinder, int size, bool ordered)
        {
            this.ordered = ordered;
            this.channelBinder = channelBinder;
        }

        public IChannel Channel
        {
            get { return channelBinder.Channel; }
        }

        public bool HasSession
        {
            get { return channelBinder.HasSession; }
        }

        public Uri ListenUri
        {
            get { return channelBinder.ListenUri; }
        }

        public EndpointAddress LocalAddress
        {
            get { return channelBinder.LocalAddress; }
        }

        public EndpointAddress RemoteAddress
        {
            get { return channelBinder.RemoteAddress; }
        }

        public void Abort()
        {
            channelBinder.Abort();
        }

        public void CloseAfterFault(TimeSpan timeout)
        {
            channelBinder.CloseAfterFault(timeout);
        }

        public Task<TryAsyncResult<RequestContext>> TryReceiveAsync(CancellationToken token)
        {
            return channelBinder.TryReceiveAsync(token);
        }

        //public bool TryReceive(TimeSpan timeout, out RequestContext requestContext)
        //{
        //    return this.channelBinder.TryReceive(timeout, out requestContext);
        //}

        //public IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback callback, object state)
        //{
        //    // At anytime there can be only one thread in BeginTryReceive and the 
        //    // outstanding AsyncResult should have completed before the next one.
        //    // There should be no pending oustanding result here.
        //    Fx.AssertAndThrow(this.outstanding == null, "BeginTryReceive should not have a pending result.");

        //    MultipleReceiveAsyncResult multipleReceiveResult = new MultipleReceiveAsyncResult(callback, state);
        //    this.outstanding = multipleReceiveResult;
        //    EnsurePump(timeout);
        //    IAsyncResult innerResult;
        //    if (this.pendingResults.TryDequeueHead(out innerResult))
        //    {
        //        HandleReceiveRequestComplete(innerResult, true);
        //    }

        //    return multipleReceiveResult;
        //}

        //void EnsurePump(TimeSpan timeout)
        //{
        //    // ensure we're running at full throttle, the BeginTryReceive calls we make below on the
        //    // IChannelBinder will typically complete future calls to BeginTryReceive made by CannelHandler
        //    // corollary to that is that most times these calls will be completed sycnhronously
        //    while (!this.pendingResults.IsFull)
        //    {
        //        ReceiveScopeSignalGate receiveScope = new ReceiveScopeSignalGate(this);

        //        // Enqueue the result without locks since this is the pump. 
        //        // BeginTryReceive can be called only from one thread and 
        //        // the head is not yet unlocked so no items can proceed.
        //        this.pendingResults.Enqueue(receiveScope);
        //        IAsyncResult result = this.channelBinder.BeginTryReceive(timeout, onInnerReceiveCompleted, receiveScope);
        //        if (result.CompletedSynchronously)
        //        {
        //            this.SignalReceiveCompleted(result);
        //        }
        //    }
        //}

        //public bool EndTryReceive(IAsyncResult result, out RequestContext requestContext)
        //{
        //    return MultipleReceiveAsyncResult.End(result, out requestContext);
        //}

        public RequestContext CreateRequestContext(Message message)
        {
            return channelBinder.CreateRequestContext(message);
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            return channelBinder.SendAsync(message, token);
        }

        public Task<Message> RequestAsync(Message message, CancellationToken token)
        {
            return channelBinder.RequestAsync(message, token);
        }

        public Task<bool> WaitForMessageAsync(CancellationToken token)
        {
            return channelBinder.WaitForMessageAsync(token);
        }
    }
}