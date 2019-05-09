using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    static class ListenerBinder
    {
        internal static IListenerBinder GetBinder(IChannelListener listener, MessageVersion messageVersion)
        {
            IChannelListener<IInputChannel> input = listener as IChannelListener<IInputChannel>;
            if (input != null)
                return new InputListenerBinder(input, messageVersion);

            IChannelListener<IInputSessionChannel> inputSession = listener as IChannelListener<IInputSessionChannel>;
            if (inputSession != null)
                return new InputSessionListenerBinder(inputSession, messageVersion);

            IChannelListener<IReplyChannel> reply = listener as IChannelListener<IReplyChannel>;
            if (reply != null)
                return new ReplyListenerBinder(reply, messageVersion);

            IChannelListener<IReplySessionChannel> replySession = listener as IChannelListener<IReplySessionChannel>;
            if (replySession != null)
                return new ReplySessionListenerBinder(replySession, messageVersion);

            IChannelListener<IDuplexChannel> duplex = listener as IChannelListener<IDuplexChannel>;
            if (duplex != null)
                return new DuplexListenerBinder(duplex, messageVersion);

            IChannelListener<IDuplexSessionChannel> duplexSession = listener as IChannelListener<IDuplexSessionChannel>;
            if (duplexSession != null)
                return new DuplexSessionListenerBinder(duplexSession, messageVersion);

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.UnknownListenerType1, listener.Uri.AbsoluteUri)));
        }

        // ------------------------------------------------------------------------------------------------------------
        // Listener Binders

        class DuplexListenerBinder : IListenerBinder
        {
            IRequestReplyCorrelator correlator;
            IChannelListener<IDuplexChannel> listener;
            MessageVersion messageVersion;

            internal DuplexListenerBinder(IChannelListener<IDuplexChannel> listener, MessageVersion messageVersion)
            {
                correlator = new RequestReplyCorrelator();
                this.listener = listener;
                this.messageVersion = messageVersion;
            }

            public IChannelListener Listener
            {
                get { return listener; }
            }

            public MessageVersion MessageVersion
            {
                get { return messageVersion; }
            }

            public async Task<IChannelBinder> AcceptAsync(CancellationToken token)
            {
                IDuplexChannel channel = await listener.AcceptChannelAsync(token);
                if (channel == null)
                    return null;
                return null;
                //return new DuplexChannelBinder(channel, correlator, listener.Uri);

            }
        }

        class DuplexSessionListenerBinder : IListenerBinder
        {
            IRequestReplyCorrelator correlator;
            IChannelListener<IDuplexSessionChannel> listener;
            MessageVersion messageVersion;

            internal DuplexSessionListenerBinder(IChannelListener<IDuplexSessionChannel> listener, MessageVersion messageVersion)
            {
                correlator = new RequestReplyCorrelator();
                this.listener = listener;
                this.messageVersion = messageVersion;
            }

            public IChannelListener Listener
            {
                get { return listener; }
            }

            public MessageVersion MessageVersion
            {
                get { return messageVersion; }
            }

            public async Task<IChannelBinder> AcceptAsync(CancellationToken token)
            {
                IDuplexSessionChannel channel = await listener.AcceptChannelAsync(token);
                if (channel == null)
                    return null;
                return null;
                //return new DuplexChannelBinder(channel, correlator, listener.Uri);
            }
        }

        class InputListenerBinder : IListenerBinder
        {
            IChannelListener<IInputChannel> listener;
            MessageVersion messageVersion;

            internal InputListenerBinder(IChannelListener<IInputChannel> listener, MessageVersion messageVersion)
            {
                this.listener = listener;
                this.messageVersion = messageVersion;
            }

            public IChannelListener Listener
            {
                get { return listener; }
            }

            public MessageVersion MessageVersion
            {
                get { return messageVersion; }
            }

            public async Task<IChannelBinder> AcceptAsync(CancellationToken token)
            {
                IInputChannel channel = await listener.AcceptChannelAsync(token);
                if (channel == null)
                    return null;

                return new InputChannelBinder(channel, listener.Uri);
            }
        }

        class InputSessionListenerBinder : IListenerBinder
        {
            IChannelListener<IInputSessionChannel> listener;
            MessageVersion messageVersion;

            internal InputSessionListenerBinder(IChannelListener<IInputSessionChannel> listener, MessageVersion messageVersion)
            {
                this.listener = listener;
                this.messageVersion = messageVersion;
            }

            public IChannelListener Listener
            {
                get { return listener; }
            }

            public MessageVersion MessageVersion
            {
                get { return messageVersion; }
            }

            public async Task<IChannelBinder> AcceptAsync(CancellationToken token)
            {
                IInputSessionChannel channel = await listener.AcceptChannelAsync(token);
                if (channel == null)
                    return null;

                return new InputChannelBinder(channel, listener.Uri);
            }
        }

        class ReplyListenerBinder : IListenerBinder
        {
            IChannelListener<IReplyChannel> listener;
            MessageVersion messageVersion;

            internal ReplyListenerBinder(IChannelListener<IReplyChannel> listener, MessageVersion messageVersion)
            {
                this.listener = listener;
                this.messageVersion = messageVersion;
            }

            public IChannelListener Listener
            {
                get { return listener; }
            }

            public MessageVersion MessageVersion
            {
                get { return messageVersion; }
            }

            public async Task<IChannelBinder> AcceptAsync(CancellationToken token)
            {
                IReplyChannel channel = await listener.AcceptChannelAsync(token);
                if (channel == null)
                    return null;
                return null;
                //return new ReplyChannelBinder(channel, listener.Uri);
            }
        }

        class ReplySessionListenerBinder : IListenerBinder
        {
            IChannelListener<IReplySessionChannel> listener;
            MessageVersion messageVersion;

            internal ReplySessionListenerBinder(IChannelListener<IReplySessionChannel> listener, MessageVersion messageVersion)
            {
                this.listener = listener;
                this.messageVersion = messageVersion;
            }

            public IChannelListener Listener
            {
                get { return listener; }
            }

            public MessageVersion MessageVersion
            {
                get { return messageVersion; }
            }

            public async Task<IChannelBinder> AcceptAsync(CancellationToken token)
            {
                IReplySessionChannel channel = await listener.AcceptChannelAsync(token);
                if (channel == null)
                    return null;
                return null;
                //return new ReplyChannelBinder(channel, listener.Uri);
            }
        }
    }
}