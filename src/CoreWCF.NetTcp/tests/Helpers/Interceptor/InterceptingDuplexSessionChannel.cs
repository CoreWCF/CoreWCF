// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;

namespace Helpers.Interceptor
{
    /// <summary>
    /// Wraps an IDuplexSessionChannel and routes every outbound and inbound message through
    /// the configured <see cref="IMessageInterceptor"/>. Messages are buffered with
    /// CreateBufferedCopy so the interceptor can examine headers/body and the original
    /// message can still be forwarded.
    /// </summary>
    /// <remarks>
    /// This class is NOT sealed: a runtime-emitted derived type adds the internal
    /// ISessionChannel&lt;IAsyncDuplexSession&gt; interface (see <see cref="InterceptorRuntimeProxies"/>),
    /// which the System.ServiceModel client reliable session binder hard-casts to via castclass.
    /// </remarks>
    public class InterceptingDuplexSessionChannel :
        IDuplexSessionChannel
    {
        private const int MaxBufferedMessageSize = 64 * 1024 * 1024;

        private readonly IDuplexSessionChannel _inner;
        private readonly IMessageInterceptor _interceptor;
        private readonly InterceptingDuplexSession _session;

        public InterceptingDuplexSessionChannel(IDuplexSessionChannel inner, IMessageInterceptor interceptor)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
            _session = InterceptorRuntimeProxies.CreateSession(_inner, this);
        }

        public EndpointAddress LocalAddress => _inner.LocalAddress;
        public EndpointAddress RemoteAddress => _inner.RemoteAddress;
        public Uri Via => _inner.Via;
        public IDuplexSession Session => _session;
        public CommunicationState State => _inner.State;

        public event EventHandler Closed { add { _inner.Closed += value; } remove { _inner.Closed -= value; } }
        public event EventHandler Closing { add { _inner.Closing += value; } remove { _inner.Closing -= value; } }
        public event EventHandler Faulted { add { _inner.Faulted += value; } remove { _inner.Faulted -= value; } }
        public event EventHandler Opened { add { _inner.Opened += value; } remove { _inner.Opened -= value; } }
        public event EventHandler Opening { add { _inner.Opening += value; } remove { _inner.Opening -= value; } }

        public T GetProperty<T>() where T : class => _inner.GetProperty<T>();

        public void Abort() => _inner.Abort();

        public void Open() => _inner.Open();
        public void Open(TimeSpan timeout) => _inner.Open(timeout);
        public IAsyncResult BeginOpen(AsyncCallback callback, object state) => _inner.BeginOpen(callback, state);
        public IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state) => _inner.BeginOpen(timeout, callback, state);
        public void EndOpen(IAsyncResult result) => _inner.EndOpen(result);

        public void Close() => _inner.Close();
        public void Close(TimeSpan timeout) => _inner.Close(timeout);
        public IAsyncResult BeginClose(AsyncCallback callback, object state) => _inner.BeginClose(callback, state);
        public IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state) => _inner.BeginClose(timeout, callback, state);
        public void EndClose(IAsyncResult result) => _inner.EndClose(result);

        // -------------- Send --------------

        public void Send(Message message) => Send(message, _inner.GetProperty<IDefaultCommunicationTimeouts>()?.SendTimeout ?? TimeSpan.FromMinutes(1));

        public void Send(Message message, TimeSpan timeout)
        {
            Message forwarded = ApplyOutbound(message);
            if (forwarded != null)
            {
                _inner.Send(forwarded, timeout);
            }
        }

        public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
        {
            Message forwarded = ApplyOutbound(message);
            if (forwarded == null)
            {
                return new DroppedSendAsyncResult(callback, state);
            }
            return _inner.BeginSend(forwarded, callback, state);
        }

        public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            Message forwarded = ApplyOutbound(message);
            if (forwarded == null)
            {
                return new DroppedSendAsyncResult(callback, state);
            }
            return _inner.BeginSend(forwarded, timeout, callback, state);
        }

        public void EndSend(IAsyncResult result)
        {
            if (result is DroppedSendAsyncResult)
            {
                return;
            }
            _inner.EndSend(result);
        }

        // -------------- Receive --------------

        public Message Receive() => ApplyInboundLoop(() => _inner.Receive());

        public Message Receive(TimeSpan timeout) => ApplyInboundLoop(() => _inner.Receive(timeout));

        public IAsyncResult BeginReceive(AsyncCallback callback, object state) => _inner.BeginReceive(callback, state);
        public IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state) => _inner.BeginReceive(timeout, callback, state);
        public Message EndReceive(IAsyncResult result)
        {
            Message message = _inner.EndReceive(result);
            return ApplyInboundOnce(message);
        }

        public bool TryReceive(TimeSpan timeout, out Message message)
        {
            if (_inner.TryReceive(timeout, out Message raw))
            {
                message = ApplyInboundOnce(raw);
                return true;
            }
            message = null;
            return false;
        }

        public IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback callback, object state) =>
            _inner.BeginTryReceive(timeout, callback, state);

        public bool EndTryReceive(IAsyncResult result, out Message message)
        {
            if (_inner.EndTryReceive(result, out Message raw))
            {
                message = ApplyInboundOnce(raw);
                return true;
            }
            message = null;
            return false;
        }

        public bool WaitForMessage(TimeSpan timeout) => _inner.WaitForMessage(timeout);
        public IAsyncResult BeginWaitForMessage(TimeSpan timeout, AsyncCallback callback, object state) =>
            _inner.BeginWaitForMessage(timeout, callback, state);
        public bool EndWaitForMessage(IAsyncResult result) => _inner.EndWaitForMessage(result);

        // -------------- Helpers --------------

        private Message ApplyOutbound(Message message)
        {
            if (message == null)
            {
                return null;
            }

            // Buffer the body so the interceptor can read it and we can still forward it.
            MessageBuffer buffer = message.CreateBufferedCopy(MaxBufferedMessageSize);
            try
            {
                using (Message forInterceptor = buffer.CreateMessage())
                {
                    InterceptDecision decision = _interceptor.OnOutbound(forInterceptor);
                    if (decision == null || decision == InterceptDecision.PassThrough)
                    {
                        return buffer.CreateMessage();
                    }
                    if (decision.Dropped)
                    {
                        return null;
                    }
                    return decision.Replacement;
                }
            }
            finally
            {
                message.Close();
            }
        }

        private Message ApplyInboundLoop(Func<Message> raw)
        {
            while (true)
            {
                Message msg = raw();
                if (msg == null)
                {
                    return null;
                }

                Message processed = ApplyInboundOnce(msg);
                if (processed != null)
                {
                    return processed;
                }
                // Dropped: try the next one.
            }
        }

        private Message ApplyInboundOnce(Message message)
        {
            if (message == null)
            {
                return null;
            }

            MessageBuffer buffer = message.CreateBufferedCopy(MaxBufferedMessageSize);
            try
            {
                using (Message forInterceptor = buffer.CreateMessage())
                {
                    InterceptDecision decision = _interceptor.OnInbound(forInterceptor);
                    if (decision == null || decision == InterceptDecision.PassThrough)
                    {
                        return buffer.CreateMessage();
                    }
                    if (decision.Dropped)
                    {
                        return null;
                    }
                    return decision.Replacement;
                }
            }
            finally
            {
                message.Close();
            }
        }

        // Synchronous IAsyncResult representing a Send that the interceptor dropped.
        private sealed class DroppedSendAsyncResult : IAsyncResult
        {
            private readonly object _state;
            private readonly System.Threading.ManualResetEvent _wait = new System.Threading.ManualResetEvent(true);

            public DroppedSendAsyncResult(AsyncCallback callback, object state)
            {
                _state = state;
                callback?.Invoke(this);
            }

            public bool IsCompleted => true;
            public System.Threading.WaitHandle AsyncWaitHandle => _wait;
            public object AsyncState => _state;
            public bool CompletedSynchronously => true;
        }
    }

    /// <summary>
    /// Wraps the inner IDuplexSession so consumers (CoreWCF reliable session layer) can
    /// call CloseOutputSession on us; we forward to the inner session which talks to the
    /// wire (and therefore goes through Send, which is intercepted).
    /// </summary>
    /// <remarks>
    /// Not sealed because <see cref="InterceptorRuntimeProxies"/> emits a derived type that
    /// adds the internal IAsyncDuplexSession interface declaration (the inherited public
    /// CloseOutputSessionAsync methods satisfy that contract by signature).
    /// </remarks>
    public class InterceptingDuplexSession : IDuplexSession
    {
        private readonly IDuplexSession _inner;
        // The inner channel may also implement ISessionChannel<IAsyncDuplexSession>; if so we
        // capture its async session via reflection so the async overloads call the real async
        // implementation rather than going through the sync APM bridge.
        private readonly object _innerAsyncSession;

        public InterceptingDuplexSession(IDuplexSessionChannel innerChannel, InterceptingDuplexSessionChannel owner)
        {
            _inner = innerChannel.Session;
            _innerAsyncSession = InterceptorRuntimeProxies.TryGetAsyncSession(innerChannel);
        }

        public string Id => _inner.Id;

        public void CloseOutputSession() => _inner.CloseOutputSession();
        public void CloseOutputSession(TimeSpan timeout) => _inner.CloseOutputSession(timeout);
        public IAsyncResult BeginCloseOutputSession(AsyncCallback callback, object state) =>
            _inner.BeginCloseOutputSession(callback, state);
        public IAsyncResult BeginCloseOutputSession(TimeSpan timeout, AsyncCallback callback, object state) =>
            _inner.BeginCloseOutputSession(timeout, callback, state);
        public void EndCloseOutputSession(IAsyncResult result) => _inner.EndCloseOutputSession(result);

        // These two methods satisfy IAsyncDuplexSession (added at runtime) by name+signature.
        // Note: the IAsyncDuplexSession contract in System.ServiceModel.Primitives 8.x is
        // CloseOutputSessionAsync() / CloseOutputSessionAsync(TimeSpan) -- no CancellationToken.
        public Task CloseOutputSessionAsync()
        {
            if (_innerAsyncSession != null)
            {
                return InterceptorRuntimeProxies.InvokeCloseOutputSessionAsync(_innerAsyncSession);
            }
            return Task.Factory.FromAsync(_inner.BeginCloseOutputSession, _inner.EndCloseOutputSession, null);
        }

        public Task CloseOutputSessionAsync(TimeSpan timeout)
        {
            if (_innerAsyncSession != null)
            {
                return InterceptorRuntimeProxies.InvokeCloseOutputSessionAsync(_innerAsyncSession, timeout);
            }
            return Task.Factory.FromAsync(
                (cb, st) => _inner.BeginCloseOutputSession(timeout, cb, st),
                _inner.EndCloseOutputSession,
                null);
        }
    }
}
