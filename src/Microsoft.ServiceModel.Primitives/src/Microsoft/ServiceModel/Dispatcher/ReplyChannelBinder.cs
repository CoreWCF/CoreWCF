using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Channels;
using Microsoft.ServiceModel.Diagnostics;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal class ReplyChannelBinder : IChannelBinder
    {
        IReplyChannel channel;
        Uri listenUri;
        bool initialized = false;

        public ReplyChannelBinder() { }

        internal void Init(IReplyChannel channel, Uri listenUri)
        {
            if (initialized)
            {
                Fx.Assert(this.channel == channel, "Wrong channel when calling Init");
                Fx.Assert(this.listenUri == listenUri, "Wrong listenUri when calling Init");
                return;
            }

            if (channel == null)
            {
                Fx.Assert("ReplyChannelBinder.ReplyChannelBinder: (channel != null)");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("channel");
            }
            this.channel = channel;
            this.listenUri = listenUri;
            initialized = true;
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
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            throw TraceUtility.ThrowHelperError(new NotImplementedException(), message);
        }

        public Task<TryAsyncResult<RequestContext>> TryReceiveAsync(CancellationToken token)
        {
            return channel.TryReceiveRequestAsync(token);
        }

        public Task<Message> RequestAsync(Message message, CancellationToken token)
        {
            throw TraceUtility.ThrowHelperError(new NotImplementedException(), message);
        }

        public Task<bool> WaitForMessageAsync(CancellationToken token)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }
    }

}