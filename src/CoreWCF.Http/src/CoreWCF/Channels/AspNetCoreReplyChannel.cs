using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Configuration;
using CoreWCF.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Channels
{
    internal class AspNetCoreReplyChannel : IReplyChannel
    {
        private IServiceProvider _serviceProvider;
        private HttpTransportSettings _httpSettings;

        public AspNetCoreReplyChannel(IServiceProvider serviceProvider, HttpTransportSettings httpSettings)
        {
            _serviceProvider = serviceProvider;
            _httpSettings = httpSettings;
        }

        // TODO: Verify what happens on .NET Framework. Looking at code it looks like it doesn't set this value
        public EndpointAddress LocalAddress => null;

        // TODO: Might want to do something a bit smarter with the state and actually have a concept of opening and closing to enable event handlers to be
        // connected and fire them when the service is shutting down.
        public CommunicationState State => CommunicationState.Created;

        public IServiceChannelDispatcher ChannelDispatcher { get; set; }

        public event EventHandler Closed;
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;

        public void Abort()
        {
            
        }

        public Task CloseAsync()
        {
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public T GetProperty<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
        }

        public Task OpenAsync()
        {
            return Task.CompletedTask;
        }

        public Task OpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public Task<RequestContext> ReceiveRequestAsync()
        {
            throw new NotImplementedException();
        }

        public Task<RequestContext> ReceiveRequestAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<(RequestContext requestContext, bool success)> TryReceiveRequestAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<bool> WaitForRequestAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        internal async Task HandleRequest(HttpContext context)
        {
            if (ChannelDispatcher == null)
            {
                // TODO: Look for existing SR which would work here. Cleanup how the exception is thrown.
                throw new InvalidOperationException("Channel Dispatcher can't be null");
            }

            var requestContext = HttpRequestContext.CreateContext(_httpSettings, context);
            var httpInput = requestContext.GetHttpInput(true);
            (Message requestMessage, Exception requestException) = await httpInput.ParseIncomingMessageAsync();
            if ((requestMessage == null) && (requestException == null))
            {
                throw Fx.Exception.AsError(
                        new ProtocolException(
                            SR.MessageXmlProtocolError,
                            new XmlException(SR.MessageIsEmpty)));
            }

            requestContext.SetMessage(requestMessage, requestException);
            if (requestMessage != null)
            {
                requestMessage.Properties.Add("Microsoft.AspNetCore.Http.HttpContext", context);
            }

            await ChannelDispatcher.DispatchAsync(requestContext);
            await requestContext.ReplySent;
        }
    }
}
