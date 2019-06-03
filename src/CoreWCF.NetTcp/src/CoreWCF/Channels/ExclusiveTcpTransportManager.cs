using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using CoreWCF.Runtime;
using CoreWCF;
using System.Threading.Tasks;
using System.Threading;

namespace CoreWCF.Channels
{
    sealed class ExclusiveTcpTransportManager : TcpTransportManager, ISocketListenerSettings
    {
        bool closed;
        ConnectionDemuxer connectionDemuxer;
        IConnectionListener connectionListener;
        IPAddress ipAddress;
        int listenBacklog;
        Socket listenSocket;
        ExclusiveTcpTransportManagerRegistration registration;

        public ExclusiveTcpTransportManager(ExclusiveTcpTransportManagerRegistration registration,
            TcpChannelListener channelListener, IPAddress ipAddressAny, UriHostNameType ipHostNameType)
        {
            ApplyListenerSettings(channelListener);

            listenSocket = channelListener.GetListenSocket(ipHostNameType);
            if (listenSocket != null)
            {
                ipAddress = ((IPEndPoint)listenSocket.LocalEndPoint).Address;
            }
            else if (channelListener.Uri.HostNameType == ipHostNameType)
            {
                ipAddress = IPAddress.Parse(channelListener.Uri.DnsSafeHost);
            }
            else
            {
                ipAddress = ipAddressAny;
            }

            listenBacklog = channelListener.ListenBacklog;
            this.registration = registration;
        }

        public IPAddress IPAddress
        {
            get
            {
                return ipAddress;
            }
        }

        public int ListenBacklog
        {
            get
            {
                return listenBacklog;
            }
        }

        int ISocketListenerSettings.BufferSize
        {
            get { return ConnectionBufferSize; }
        }

        int ISocketListenerSettings.ListenBacklog
        {
            get { return ListenBacklog; }
        }

        internal override Task OnOpenAsync()
        {
            SocketConnectionListener socketListener = null;

            if (listenSocket != null)
            {
                socketListener = new SocketConnectionListener(listenSocket, this, false);
                listenSocket = null;
            }
            else
            {
                int port = registration.ListenUri.Port;
                if (port == -1)
                    port = TcpUri.DefaultPort;

                socketListener = new SocketConnectionListener(new IPEndPoint(ipAddress, port), this, false);
            }

            connectionListener = new BufferedConnectionListener(socketListener, MaxOutputDelay, ConnectionBufferSize);
            connectionDemuxer = new ConnectionDemuxer(connectionListener,
                MaxPendingAccepts, MaxPendingConnections, ChannelInitializationTimeout,
                IdleTimeout, MaxPooledConnections,
                OnGetTransportFactorySettings,
                OnGetSingletonMessageHandler,
                OnHandleServerSessionPreamble,
                OnDemuxerError);

            bool startedDemuxing = false;
            try
            {
                connectionDemuxer.StartDemuxing();
                startedDemuxing = true;
            }
            finally
            {
                if (!startedDemuxing)
                {
                    connectionDemuxer.Dispose();
                }
            }

            return Task.CompletedTask;
        }

        internal override Task OnCloseAsync(CancellationToken token)
        {
            if(token.IsCancellationRequested)
            {
                return Task.FromCanceled(token);
            }
            
            Cleanup();
            return Task.CompletedTask;
        }

        internal override void OnAbort()
        {
            Cleanup();
            base.OnAbort();
        }

        void Cleanup()
        {
            lock (ThisLock)
            {
                if (closed)
                {
                    return;
                }

                closed = true;
            }

            if (connectionDemuxer != null)
            {
                connectionDemuxer.Dispose();
            }

            if (connectionListener != null)
            {
                connectionListener.Dispose();
            }

            registration.OnClose(this);
        }
    }

    class ExclusiveTcpTransportManagerRegistration : TransportManagerRegistration
    {
        int connectionBufferSize;
        TimeSpan channelInitializationTimeout;
        TimeSpan idleTimeout;
        int maxPooledConnections;
        int listenBacklog;
        TimeSpan maxOutputDelay;
        int maxPendingConnections;
        int maxPendingAccepts;

        ExclusiveTcpTransportManager ipv4TransportManager;
        ExclusiveTcpTransportManager ipv6TransportManager;

        public ExclusiveTcpTransportManagerRegistration(Uri listenUri, TcpChannelListener channelListener)
            : base(listenUri, channelListener.HostNameComparisonMode)
        {
            connectionBufferSize = channelListener.ConnectionBufferSize;
            channelInitializationTimeout = channelListener.ChannelInitializationTimeout;
            listenBacklog = channelListener.ListenBacklog;
            maxOutputDelay = channelListener.MaxOutputDelay;
            maxPendingConnections = channelListener.MaxPendingConnections;
            maxPendingAccepts = channelListener.MaxPendingAccepts;
            idleTimeout = channelListener.IdleTimeout;
            maxPooledConnections = channelListener.MaxPooledConnections;
        }

        public void OnClose(TcpTransportManager manager)
        {
            if (manager == ipv4TransportManager)
            {
                ipv4TransportManager = null;
            }
            else if (manager == ipv6TransportManager)
            {
                ipv6TransportManager = null;
            }
            else
            {
                Fx.Assert("Unknown transport manager passed to OnClose().");
            }

            if ((ipv4TransportManager == null) && (ipv6TransportManager == null))
            {
                TcpChannelListener.StaticTransportManagerTable.UnregisterUri(ListenUri, HostNameComparisonMode);
            }
        }

        bool IsCompatible(TcpChannelListener channelListener, bool useIPv4, bool useIPv6)
        {
            if (channelListener.InheritBaseAddressSettings)
                return true;

            if (useIPv6)
            {
                if (!channelListener.IsScopeIdCompatible(HostNameComparisonMode, ListenUri))
                {
                    return false;
                }
            }

            return (/*!channelListener.PortSharingEnabled
                &&*/ (useIPv4 || useIPv6)
                && (channelInitializationTimeout == channelListener.ChannelInitializationTimeout)
                && (idleTimeout == channelListener.IdleTimeout)
                && (maxPooledConnections == channelListener.MaxPooledConnections)
                && (connectionBufferSize == channelListener.ConnectionBufferSize)
                && (listenBacklog == channelListener.ListenBacklog)
                && (maxPendingConnections == channelListener.MaxPendingConnections)
                && (maxOutputDelay == channelListener.MaxOutputDelay)
                && (maxPendingAccepts == channelListener.MaxPendingAccepts));
        }

        void ProcessSelection(TcpChannelListener channelListener, IPAddress ipAddressAny, UriHostNameType ipHostNameType,
            ref ExclusiveTcpTransportManager transportManager, IList<TransportManager> result)
        {
            if (transportManager == null)
            {
                transportManager = new ExclusiveTcpTransportManager(this, channelListener, ipAddressAny, ipHostNameType);
            }
            result.Add(transportManager);
        }

        public override IList<TransportManager> Select(TransportChannelListener channelListener)
        {
            bool useIPv4 = (ListenUri.HostNameType != UriHostNameType.IPv6) && Socket.OSSupportsIPv4;
            bool useIPv6 = (ListenUri.HostNameType != UriHostNameType.IPv4) && Socket.OSSupportsIPv6;

            TcpChannelListener tcpListener = (TcpChannelListener)channelListener;
            if (!IsCompatible(tcpListener, useIPv4, useIPv6))
            {
                return null;
            }

            IList<TransportManager> result = new List<TransportManager>();
            if (useIPv4)
            {
                ProcessSelection(tcpListener, IPAddress.Any, UriHostNameType.IPv4,
                    ref ipv4TransportManager, result);
            }
            if (useIPv6)
            {
                ProcessSelection(tcpListener, IPAddress.IPv6Any, UriHostNameType.IPv6,
                    ref ipv6TransportManager, result);
            }
            return result;
        }
    }
}
