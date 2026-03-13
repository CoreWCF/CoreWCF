// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Runtime;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Channels
{
    internal class AspNetCoreReplyChannel : IReplyChannel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly HttpTransportSettings _httpSettings;

        public AspNetCoreReplyChannel(IServiceProvider serviceProvider, HttpTransportSettings httpSettings)
        {
            _serviceProvider = serviceProvider;
            _httpSettings = httpSettings;
        }

        // TODO: Verify what happens on .NET Framework. Looking at code it looks like it doesn't set this value
        public EndpointAddress LocalAddress => null;

        // TODO: Might want to do something a bit smarter with the state and actually have a concept of opening and closing to enable event handlers to be
        // connected and fire them when the service is shutting down.
        public CommunicationState State { get; private set; } = CommunicationState.Created;

        public IServiceChannelDispatcher ChannelDispatcher { get; set; }

#pragma warning disable CS0067 // The event is never used - see issue #290
        public event EventHandler Closed;
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;
#pragma warning restore CS0067 // The event is never used

        public void Abort()
        {
            // Can skip Closing state as there's nothing to do during the Abort call
            State = CommunicationState.Closed;
        }

        public Task CloseAsync()
        {
            // Can skip Closing state as there's nothing to do during the Close call
            State = CommunicationState.Closed;
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken token)
        {
            // Can skip Closing state as there's nothing to do during the Close call
            State = CommunicationState.Closed;
            return Task.CompletedTask;
        }

        public T GetProperty<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
        }

        public Task OpenAsync()
        {
            // Can skip Opening state as there's nothing to do during the Open call
            State = CommunicationState.Opened;
            return Task.CompletedTask;
        }

        public Task OpenAsync(CancellationToken token)
        {
            // Can skip Opening state as there's nothing to do during the Open call
            State = CommunicationState.Opened;
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

            using (var requestContext = HttpRequestContext.CreateContext(_httpSettings, context))
            {
                bool authenticationResult = await requestContext.ProcessAuthenticationAsync();
                if (!authenticationResult)
                {
                    return;
                }

                PipeReader reader = null;
                try
                {
                    HttpInput httpInput = requestContext.GetHttpInput(true);
                    (Message requestMessage, Exception requestException, reader) = await httpInput.ParseIncomingMessageAsync();
                    if ((requestMessage == null) && (requestException == null))
                    {
                        await requestContext.SendResponseAndCloseAsync(System.Net.HttpStatusCode.BadRequest);
                        return;
                    }

                    requestContext.SetMessage(requestMessage, requestException);
                    if (requestMessage != null)
                    {
                        requestMessage.Properties.Add("Microsoft.AspNetCore.Http.HttpContext", context);
                    }

                    await ChannelDispatcher.DispatchAsync(requestContext);
                    await requestContext.ReplySent;
                }
                catch (Exception ex)
                {
                    await HandleProcessInboundException(ex, requestContext);
                }
                finally
                {
                    // We need to call CompleteAsync after DispatchAsync to avoid internal buffers being freed.
                    // Once migrated to .NET6+ we will probably inspect the body response using HttpContext.Request.BodyReader
                    // .. in that case should the reader be completed ?
                    if (reader is not null)
                    {
                        await reader.CompleteAsync();
                    }
                }
            }
        }

        private static async Task HandleProcessInboundException(Exception ex, HttpRequestContext requestContext)
        {
            if (Fx.IsFatal(ex))
            {
                return;
            }

            if (ex is ProtocolException)
            {
                ProtocolException protocolException = (ProtocolException)ex;
                HttpStatusCode statusCode = HttpStatusCode.BadRequest;
                string statusDescription = string.Empty;
                if (protocolException.Data.Contains(HttpChannelUtilities.HttpStatusCodeExceptionKey))
                {
                    statusCode = (HttpStatusCode)protocolException.Data[HttpChannelUtilities.HttpStatusCodeExceptionKey];
                    protocolException.Data.Remove(HttpChannelUtilities.HttpStatusCodeExceptionKey);
                }
                if (protocolException.Data.Contains(HttpChannelUtilities.HttpStatusDescriptionExceptionKey))
                {
                    statusDescription = (string)protocolException.Data[HttpChannelUtilities.HttpStatusDescriptionExceptionKey];
                    protocolException.Data.Remove(HttpChannelUtilities.HttpStatusDescriptionExceptionKey);
                }
                await requestContext.SendResponseAndCloseAsync(statusCode, statusDescription);
            }
            else
            {
                await requestContext.SendResponseAndCloseAsync(HttpStatusCode.BadRequest);
            }
        }
    }
}
