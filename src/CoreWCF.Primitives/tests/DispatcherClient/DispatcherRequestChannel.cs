using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading;
using System.Threading.Tasks;

namespace DispatcherClient
{
    internal class DispatcherRequestChannel : CommunicationObject, IRequestChannel
    {
        private IServiceProvider _serviceProvider;
        private IServiceChannelDispatcher _serviceChannelDispatch;

        public DispatcherRequestChannel(IServiceProvider serviceProvider, EndpointAddress to, Uri via)
        {
            _serviceProvider = serviceProvider;
            RemoteAddress = to;
            Via = via;
        }

        public EndpointAddress RemoteAddress { get; }
        public Uri Via { get; }
        protected override TimeSpan DefaultCloseTimeout => Factory.CloseTimeout;
        protected override TimeSpan DefaultOpenTimeout => Factory.OpenTimeout;
        protected TimeSpan DefaultSendTimeout => Factory.SendTimeout;


        private DispatcherChannelFactory Factory => _serviceProvider.GetRequiredService<DispatcherChannelFactory>();
        public IAsyncResult BeginRequest(Message message, AsyncCallback callback, object state)
        {
            return BeginRequest(message, DefaultSendTimeout, callback, state);
        }

        public IAsyncResult BeginRequest(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            var tcs = new TaskCompletionSource<Message>(state);
            RequestAsync(message, timeout).ContinueWith((antecedant) =>
            {
                if (antecedant.IsFaulted)
                {
                    tcs.TrySetException(antecedant.Exception);
                }
                else
                {
                    tcs.TrySetResult(antecedant.Result);
                }
            });
            return tcs.Task;
        }

        public Message EndRequest(IAsyncResult result)
        {
            var task = (Task<Message>)result;
            return task.Result;
        }

        public async Task<Message> RequestAsync(Message message, TimeSpan timeout)
        {
            RemoteAddress?.ApplyTo(message);
            var requestContext = new DispatcherClientRequestContext(message);
            var cts = new CancellationTokenSource(timeout);
            await _serviceChannelDispatch.DispatchAsync(requestContext, cts.Token);
            var replyMessage = Helpers.TestHelper.ConvertMessage(requestContext.ReplyMessage);
            cts.Dispose();
            return replyMessage;
        }

        public T GetProperty<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
        }

        public Message Request(Message message)
        {
            return Request(message, DefaultSendTimeout);
        }

        public Message Request(Message message, TimeSpan timeout)
        {
            return RequestAsync(message, timeout).GetAwaiter().GetResult();
        }

        protected override void OnAbort()
        {
        }

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            var tcs = new TaskCompletionSource<object>(state);
            OnCloseAsync(timeout).ContinueWith(
                (antecedant) =>
                {
                    if (antecedant.IsFaulted)
                    {
                        tcs.TrySetException(antecedant.Exception);
                    }
                    else
                    {
                        tcs.TrySetResult(null);
                    }
                });
            return tcs.Task;
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            OnOpen(timeout);
            return Task.CompletedTask;
        }

        protected override void OnClose(TimeSpan timeout)
        {
            OnCloseAsync(timeout).GetAwaiter().GetResult();
        }

        private Task OnCloseAsync(TimeSpan timeout)
        {
            IServiceChannelDispatcher serviceChannelDispatch = Factory.GetServiceChannelDispatcher(this);
            return serviceChannelDispatch.DispatchAsync(null, CancellationToken.None);
        }

        protected override void OnEndClose(IAsyncResult result)
        {
            ((Task)result).GetAwaiter().GetResult();
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
        }

        protected override void OnOpen(TimeSpan timeout)
        {
            _serviceChannelDispatch = Factory.GetServiceChannelDispatcher(this);
        }
    }
}
