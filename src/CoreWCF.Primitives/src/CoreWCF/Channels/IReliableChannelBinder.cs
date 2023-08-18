// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;

namespace CoreWCF.Channels
{
    internal delegate void BinderExceptionHandler(IReliableChannelBinder sender, Exception exception);

    internal interface IReliableChannelBinder
    {
        IChannel Channel { get; }
        bool Connected { get; }
        TimeSpan DefaultSendTimeout { get; }
        bool HasSession { get; }
        EndpointAddress LocalAddress { get; }
        EndpointAddress RemoteAddress { get; }
        CommunicationState State { get; }

        event BinderExceptionHandler Faulted;
        event BinderExceptionHandler OnException;

        void Abort();

        Task CloseAsync(CancellationToken token);
        Task CloseAsync(CancellationToken token, MaskingMode maskingMode);
        Task OpenAsync(CancellationToken token);
        Task SendAsync(Message message, CancellationToken token);
        Task SendAsync(Message message, CancellationToken token, MaskingMode maskingMode);

        //Task DispatchAsync(RequestContext context);
        //Task DispatchAsync(RequestContext context, MaskingMode maskingMode);
        Task<bool> StartTryGetChannelAsync();
        Task OnReceivedMessageAsync();
        Task<RequestContext> OnReceivedRequestAsync(RequestContext context);
        Task ReceivedRequestOnChannelAsync(IChannel channel);
        ISession GetInnerSession();
        void HandleException(Exception e);
        bool IsHandleable(Exception e);
        void SetMaskingMode(RequestContext context, MaskingMode maskingMode);
        RequestContext WrapRequestContext(RequestContext context);
    }

    internal interface IServerReliableChannelBinder : IReliableChannelBinder
    {
        bool AddressResponse(Message request, Message response);
        bool UseNewChannel(IChannel channel);

        //Task<Message> RequestAsync(Message message, TimeSpan timeout);
        //Task<Message> RequestAsync(Message message, TimeSpan timeout, MaskingMode maskingMode);
    }
}
