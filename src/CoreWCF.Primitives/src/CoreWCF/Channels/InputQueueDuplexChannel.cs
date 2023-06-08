// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class InputQueueDuplexChannel : InputQueueServiceChannelDispatcher<Message>, IDuplexChannel
    {
        private readonly EndpointAddress _localAddress;

        protected InputQueueDuplexChannel(ChannelManagerBase channelManager, EndpointAddress localAddress)
            : base(channelManager)
        {
            _localAddress = localAddress;
        }

        public virtual EndpointAddress LocalAddress => _localAddress;
        public abstract EndpointAddress RemoteAddress { get; }
        public abstract Uri Via { get; }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IDuplexChannel))
            {
                return (T)(object)this;
            }

            T baseProperty = base.GetProperty<T>();
            if (baseProperty != null)
            {
                return baseProperty;
            }

            return default(T);
        }

        protected abstract Task OnSendAsync(Message message, CancellationToken token);

        public Task SendAsync(Message message)
        {
            return SendAsync (message, new TimeoutHelper(DefaultSendTimeout).GetCancellationToken());
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            if (message == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));

            ThrowIfDisposedOrNotOpen();

            AddHeadersTo(message);
            return OnSendAsync(message, token);
        }

        protected virtual void AddHeadersTo(Message message) { }

        public Task<Message> ReceiveAsync(CancellationToken token) => throw new NotImplementedException();
        public Task<(Message message, bool success)> TryReceiveAsync(CancellationToken token) => throw new NotImplementedException();

        //public Message Receive()
        //{
        //    return Receive(DefaultReceiveTimeout);
        //}

        //public Message Receive(TimeSpan timeout)
        //{
        //    if (timeout < TimeSpan.Zero)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //            new ArgumentOutOfRangeException(nameof(timeout), timeout, SR.SFxTimeoutOutOfRange0));

        //    ThrowPending();
        //    return InputChannel.HelpReceive(this, timeout);
        //}

        //public IAsyncResult BeginReceive(AsyncCallback callback, object state)
        //{
        //    return BeginReceive(DefaultReceiveTimeout, callback, state);
        //}

        //public IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
        //{
        //    if (timeout < TimeSpan.Zero)
        //    {
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //            new ArgumentOutOfRangeException(nameof(timeout), timeout, SR.SFxTimeoutOutOfRange0));
        //    }

        //    ThrowPending();
        //    return InputChannel.HelpBeginReceive(this, timeout, callback, state);
        //}

        //public Message EndReceive(IAsyncResult result)
        //{
        //    return InputChannel.HelpEndReceive(result);
        //}

        //public bool TryReceive(TimeSpan timeout, out Message message)
        //{
        //    if (timeout < TimeSpan.Zero)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //            new ArgumentOutOfRangeException(nameof(timeout), timeout, SR.SFxTimeoutOutOfRange0));

        //    ThrowPending();
        //    return base.Dequeue(timeout, out message);
        //}

        //public IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback callback, object state)
        //{
        //    if (timeout < TimeSpan.Zero)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //            new ArgumentOutOfRangeException(nameof(timeout), timeout, SR.SFxTimeoutOutOfRange0));

        //    ThrowPending();
        //    return base.BeginDequeue(timeout, callback, state);
        //}

        //public bool EndTryReceive(IAsyncResult result, out Message message)
        //{
        //    return base.EndDequeue(result, out message);
        //}

        //public bool WaitForMessage(TimeSpan timeout)
        //{
        //    if (timeout < TimeSpan.Zero)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //            new ArgumentOutOfRangeException(nameof(timeout), timeout, SR.SFxTimeoutOutOfRange0));

        //    ThrowPending();
        //    return base.WaitForItem(timeout);
        //}

        //public IAsyncResult BeginWaitForMessage(TimeSpan timeout, AsyncCallback callback, object state)
        //{
        //    if (timeout < TimeSpan.Zero)
        //        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
        //            new ArgumentOutOfRangeException(nameof(timeout), timeout, SR.SFxTimeoutOutOfRange0));

        //    ThrowPending();
        //    return base.BeginWaitForItem(timeout, callback, state);
        //}

        //public bool EndWaitForMessage(IAsyncResult result)
        //{
        //    return base.EndWaitForItem(result);
        //}
    }
}
