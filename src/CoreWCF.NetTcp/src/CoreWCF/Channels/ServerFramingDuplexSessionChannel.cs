using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Runtime;
using CoreWCF.Security;
using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels.Framing;

namespace CoreWCF.Channels
{
    internal class ServerFramingDuplexSessionChannel : FramingDuplexSessionChannel
    {
        StreamUpgradeAcceptor upgradeAcceptor;
        private IServiceProvider _serviceProvider;
        IStreamUpgradeChannelBindingProvider channelBindingProvider;

        public ServerFramingDuplexSessionChannel(FramingConnection connection, ITransportFactorySettings settings,
            bool exposeConnectionProperty, IServiceProvider serviceProvider)
            : base(connection, settings, exposeConnectionProperty)
        {
            Connection = connection;
            upgradeAcceptor = connection.StreamUpgradeAcceptor;
            _serviceProvider = serviceProvider;
            //if (upgradeAcceptor != null)
            //{
            //    this.channelBindingProvider = upgrade.GetProperty<IStreamUpgradeChannelBindingProvider>();
            //    this.upgradeAcceptor = upgrade.CreateUpgradeAcceptor();
            //}
        }

        protected override void ReturnConnectionIfNecessary(bool abort, CancellationToken token)
        {
            // TODO: Put connection back into the beginning of the middleware stack
            //    IConnection localConnection = null;
            //    if (this.sessionReader != null)
            //    {
            //        lock (ThisLock)
            //        {
            //            localConnection = this.sessionReader.GetRawConnection();
            //        }
            //    }

            //    if (localConnection != null)
            //    {
            //        if (abort)
            //        {
            //            localConnection.Abort();
            //        }
            //        else
            //        {
            //            this.connectionDemuxer.ReuseConnection(localConnection, timeout);
            //        }
            //        this.connectionDemuxer = null;
            //    }
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IChannelBindingProvider))
            {
                return (T)(object)channelBindingProvider;
            }

            T service = _serviceProvider.GetService<T>();
            if (service != null)
            {
                return service;
            }

            return base.GetProperty<T>();
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask; // NOOP
        }

    }

    internal abstract class FramingDuplexSessionChannel : TransportDuplexSessionChannel
    {
        bool exposeConnectionProperty;

        private FramingDuplexSessionChannel(ITransportFactorySettings settings,
            EndpointAddress localAddress, Uri localVia, EndpointAddress remoteAddress, Uri via, bool exposeConnectionProperty)
            : base(settings, localAddress, localVia, remoteAddress, via)
        {
            this.exposeConnectionProperty = exposeConnectionProperty;
        }

        protected FramingDuplexSessionChannel(FramingConnection connection, ITransportFactorySettings settings, bool exposeConnectionProperty)
    : this(settings, new EndpointAddress(connection.ServiceDispatcher.BaseAddress), connection.Via,
    EndpointAddress.AnonymousAddress, connection.MessageEncoder.MessageVersion.Addressing.AnonymousUri, exposeConnectionProperty)
        {
            Session = FramingConnectionDuplexSession.CreateSession(this, connection.StreamUpgradeAcceptor);
        }

        protected FramingConnection Connection { get; set; }

        protected override bool IsStreamedOutput
        {
            get { return false; }
        }

        protected override async Task CloseOutputSessionCoreAsync(CancellationToken token)
        {
            Connection.RawStream?.StartUnwrapRead();
            try
            {
                await Connection.Output.WriteAsync(SessionEncoder.EndBytes, token);
                await Connection.Output.FlushAsync();
            }
            finally
            {
                if (Connection.RawStream != null)
                {
                    Connection.RawStream.FinishUnwrapRead();
                    Connection.RawStream = null;
                    Connection.Output.Complete();
                    Connection.Input.Complete();
                }
            }
        }

        protected override Task CompleteCloseAsync(CancellationToken token)
        {
            ReturnConnectionIfNecessary(false, token);
            return Task.CompletedTask;
        }

        protected override async Task OnSendCoreAsync(Message message, CancellationToken token)
        {
            bool allowOutputBatching;
            ArraySegment<byte> messageData;
            allowOutputBatching = message.Properties.AllowOutputBatching;
            messageData = EncodeMessage(message);
            await Connection.Output.WriteAsync(messageData, token);
            await Connection.Output.FlushAsync();
        }

        protected override async Task CloseOutputAsync(CancellationToken token)
        {
            await Connection.Output.WriteAsync(SessionEncoder.EndBytes, token);
            await Connection.Output.FlushAsync();
        }

        protected override ArraySegment<byte> EncodeMessage(Message message)
        {
            ArraySegment<byte> messageData = MessageEncoder.WriteMessage(message,
                int.MaxValue, BufferManager, SessionEncoder.MaxMessageFrameSize);

            messageData = SessionEncoder.EncodeMessageFrame(messageData);

            return messageData;
        }

        class FramingConnectionDuplexSession : ConnectionDuplexSession
        {

            FramingConnectionDuplexSession(FramingDuplexSessionChannel channel)
                : base(channel)
            {
            }

            public static FramingConnectionDuplexSession CreateSession(FramingDuplexSessionChannel channel,
                StreamUpgradeAcceptor upgradeAcceptor)
            {
                StreamSecurityUpgradeAcceptor security = upgradeAcceptor as StreamSecurityUpgradeAcceptor;
                if (security == null)
                {
                    return new FramingConnectionDuplexSession(channel);
                }
                else
                {
                    return new SecureConnectionDuplexSession(channel);
                }
            }

            class SecureConnectionDuplexSession : FramingConnectionDuplexSession, ISecuritySession
            {
                EndpointIdentity remoteIdentity;

                public SecureConnectionDuplexSession(FramingDuplexSessionChannel channel)
                    : base(channel)
                {
                    // empty
                }

                EndpointIdentity ISecuritySession.RemoteIdentity
                {
                    get
                    {
                        if (remoteIdentity == null)
                        {
                            SecurityMessageProperty security = Channel.RemoteSecurity;
                            if (security != null && security.ServiceSecurityContext != null &&
                                security.ServiceSecurityContext.IdentityClaim != null &&
                                security.ServiceSecurityContext.PrimaryIdentity != null)
                            {
                                remoteIdentity = EndpointIdentity.CreateIdentity(
                                    security.ServiceSecurityContext.IdentityClaim);
                            }
                        }

                        return remoteIdentity;
                    }
                }
            }
        }
    }
}
