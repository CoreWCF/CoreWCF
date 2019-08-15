using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;

namespace CoreWCF.Dispatcher
{
    internal class InputChannelBinder : IChannelBinder
    {
        IInputChannel channel;
        Uri listenUri;

        public InputChannelBinder()
        {

        }

        internal void Init(IInputChannel channel, Uri listenUri)
        {
            if (!((channel != null)))
            {
                Fx.Assert("InputChannelBinder.InputChannelBinder: (channel != null)");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channel));
            }
            this.channel = channel;
            this.listenUri = listenUri;
        }

        public IChannel Channel
        {
            get { return channel; }
        }

        public bool HasSession
        {
            get { return channel is ISessionChannel<IInputSession>; }
        }

        public Uri ListenUri
        {
            get { return listenUri; }
        }

        public EndpointAddress LocalAddress
        {
            get { return channel.LocalAddress; }
        }

        public EndpointAddress RemoteAddress
        {
            get
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
            }
        }

        public void Abort()
        {
            channel.Abort();
        }

        public void CloseAfterFault(TimeSpan timeout)
        {
            var helper = new TimeoutHelper(timeout);
            channel.CloseAsync(helper.GetCancellationToken()).GetAwaiter().GetResult();
        }

        public RequestContext CreateRequestContext(Message message)
        {
            return WrapMessage(message);
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            throw TraceUtility.ThrowHelperError(new NotImplementedException(), message);
        }

        public async Task<TryAsyncResult<RequestContext>> TryReceiveAsync(CancellationToken token)
        {
            var result = await channel.TryReceiveAsync(token);
            if (result.Success)
            {
                return TryAsyncResult.FromResult(WrapMessage(result.Result));
            }
            else
            {
                return TryAsyncResult<RequestContext>.FailedResult;
            }
        }

        public Task<Message> RequestAsync(Message message, CancellationToken token)
        {
            throw TraceUtility.ThrowHelperError(new NotImplementedException(), message);
        }

        public Task<bool> WaitForMessageAsync(CancellationToken token)
        {
            return channel.WaitForMessageAsync(token);
        }

        RequestContext WrapMessage(Message message)
        {
            if (message == null)
            {
                return null;
            }
            else
            {
                return new InputRequestContext(message, this);
            }
        }

        class InputRequestContext : RequestContextBase
        {
            InputChannelBinder binder;

            internal InputRequestContext(Message request, InputChannelBinder binder)
                : base(request, TimeSpan.Zero, TimeSpan.Zero)
            {
                this.binder = binder;
            }

            protected override void OnAbort()
            {
            }

            protected override Task OnCloseAsync(CancellationToken token)
            {
                return Task.CompletedTask;
            }

            protected override Task OnReplyAsync(Message message, CancellationToken token)
            {
                return Task.CompletedTask;
            }
        }
    }

}