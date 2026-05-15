// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace Helpers.Interceptor
{
    internal sealed class InterceptingChannelFactory : IChannelFactory<IDuplexSessionChannel>
    {
        private readonly IChannelFactory<IDuplexSessionChannel> _inner;
        private readonly IMessageInterceptor _interceptor;

        public InterceptingChannelFactory(IChannelFactory<IDuplexSessionChannel> inner, IMessageInterceptor interceptor)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _interceptor = interceptor ?? throw new ArgumentNullException(nameof(interceptor));
        }

        public IDuplexSessionChannel CreateChannel(EndpointAddress to, Uri via) =>
            InterceptorRuntimeProxies.CreateChannel(_inner.CreateChannel(to, via), _interceptor);

        public IDuplexSessionChannel CreateChannel(EndpointAddress to) =>
            InterceptorRuntimeProxies.CreateChannel(_inner.CreateChannel(to), _interceptor);

        public T GetProperty<T>() where T : class => _inner.GetProperty<T>();

        public CommunicationState State => _inner.State;

        public event EventHandler Closed { add { _inner.Closed += value; } remove { _inner.Closed -= value; } }
        public event EventHandler Closing { add { _inner.Closing += value; } remove { _inner.Closing -= value; } }
        public event EventHandler Faulted { add { _inner.Faulted += value; } remove { _inner.Faulted -= value; } }
        public event EventHandler Opened { add { _inner.Opened += value; } remove { _inner.Opened -= value; } }
        public event EventHandler Opening { add { _inner.Opening += value; } remove { _inner.Opening -= value; } }

        public void Abort() => _inner.Abort();

        public void Close() => _inner.Close();
        public void Close(TimeSpan timeout) => _inner.Close(timeout);

        public IAsyncResult BeginClose(AsyncCallback callback, object state) => _inner.BeginClose(callback, state);
        public IAsyncResult BeginClose(TimeSpan timeout, AsyncCallback callback, object state) => _inner.BeginClose(timeout, callback, state);
        public void EndClose(IAsyncResult result) => _inner.EndClose(result);

        public void Open() => _inner.Open();
        public void Open(TimeSpan timeout) => _inner.Open(timeout);

        public IAsyncResult BeginOpen(AsyncCallback callback, object state) => _inner.BeginOpen(callback, state);
        public IAsyncResult BeginOpen(TimeSpan timeout, AsyncCallback callback, object state) => _inner.BeginOpen(timeout, callback, state);
        public void EndOpen(IAsyncResult result) => _inner.EndOpen(result);
    }
}
