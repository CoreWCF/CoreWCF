using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime;
using System.Security.Authentication.ExtendedProtection;
using CoreWCF;
using CoreWCF.Description;
using CoreWCF.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using System;
using CoreWCF.Channels;

namespace CoreWCF.Channels
{
    static class TcpUri
    {
        public const int DefaultPort = 808;
    }

    abstract class TcpChannelListener<TChannel, TChannelAcceptor>
        : TcpChannelListener, IChannelListener<TChannel>
        where TChannel : class, IChannel
        where TChannelAcceptor : ChannelAcceptor<TChannel>
    {
        protected TcpChannelListener(TcpTransportBindingElement bindingElement, BindingContext context)
            : base(bindingElement, context)
        {
        }

        protected abstract TChannelAcceptor ChannelAcceptor { get; }

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            await base.OnOpenAsync(token);
            token.ThrowIfCancellationRequested();
            await ChannelAcceptor.OpenAsync(token);
        }

        protected override async Task OnCloseAsync(CancellationToken token)
        {
            await ChannelAcceptor.CloseAsync(token);
            await base.OnCloseAsync(token);
        }

        protected override void OnAbort()
        {
            ChannelAcceptor.Abort();
            base.OnAbort();
        }

        public Task<TChannel> AcceptChannelAsync()
        {
            return AcceptChannelAsync(new TimeoutHelper(DefaultReceiveTimeout).GetCancellationToken());
        }

        public Task<TChannel> AcceptChannelAsync(CancellationToken token)
        {
            base.ThrowIfNotOpened();
            return ChannelAcceptor.AcceptChannelAsync(token);
        }
    }

    class TcpReplyChannelListener
        : TcpChannelListener<IReplyChannel, ReplyChannelAcceptor>, ISingletonChannelListener
    {
        ReplyChannelAcceptor replyAcceptor;

        public TcpReplyChannelListener(TcpTransportBindingElement bindingElement, BindingContext context)
            : base(bindingElement, context)
        {
            replyAcceptor = new ConnectionOrientedTransportReplyChannelAcceptor(this);
        }

        protected override ReplyChannelAcceptor ChannelAcceptor
        {
            get { return replyAcceptor; }
        }

        TimeSpan ISingletonChannelListener.ReceiveTimeout
        {
            get { return DefaultReceiveTimeout; }
        }

        void ISingletonChannelListener.ReceiveRequest(RequestContext requestContext, Action callback, bool canDispatchOnThisThread)
        {
            replyAcceptor.Enqueue(requestContext, callback, canDispatchOnThisThread);
        }
    }

    class TcpDuplexChannelListener
        : TcpChannelListener<IDuplexSessionChannel, InputQueueChannelAcceptor<IDuplexSessionChannel>>, ISessionPreambleHandler
    {
        InputQueueChannelAcceptor<IDuplexSessionChannel> duplexAcceptor;

        public TcpDuplexChannelListener(TcpTransportBindingElement bindingElement, BindingContext context)
            : base(bindingElement, context)
        {
            duplexAcceptor = new InputQueueChannelAcceptor<IDuplexSessionChannel>(this);
        }

        protected override InputQueueChannelAcceptor<IDuplexSessionChannel> ChannelAcceptor
        {
            get { return duplexAcceptor; }
        }

        void ISessionPreambleHandler.HandleServerSessionPreamble(ServerSessionPreambleConnectionReader preambleReader,
            ConnectionDemuxer connectionDemuxer)
        {
            IDuplexSessionChannel channel = preambleReader.CreateDuplexSessionChannel(
                this, new EndpointAddress(Uri), ExposeConnectionProperty, connectionDemuxer);

            duplexAcceptor.EnqueueAndDispatch(channel, preambleReader.ConnectionDequeuedCallback);
        }
    }

    abstract class TcpChannelListener : ConnectionOrientedTransportChannelListener
    {
        int listenBacklog;

        // "port 0" support
        Socket ipv4ListenSocket;
        Socket ipv6ListenSocket;
        ExtendedProtectionPolicy extendedProtectionPolicy;
        static Random randomPortGenerator = new Random(AppDomain.CurrentDomain.GetHashCode() | Environment.TickCount);

        static UriPrefixTable<ITransportManagerRegistration> transportManagerTable =
            new UriPrefixTable<ITransportManagerRegistration>(true);

        protected TcpChannelListener(TcpTransportBindingElement bindingElement, BindingContext context)
            : base(bindingElement, context)
        {
            listenBacklog = bindingElement.ListenBacklog;
            extendedProtectionPolicy = bindingElement.ExtendedProtectionPolicy;
            SetIdleTimeout(ConnectionOrientedTransportDefaults.IdleTimeout);
            InitializeMaxPooledConnections();

            // for exclusive mode, we have "port 0" functionality
            if (context.ListenUriMode == ListenUriMode.Unique)
            {
                SetupUniquePort(context);
            }
        }

        public int ListenBacklog
        {
            get
            {
                return listenBacklog;
            }
        }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(ExtendedProtectionPolicy))
            {
                return (T)(object)extendedProtectionPolicy;
            }

            return base.GetProperty<T>();
        }

        internal Socket GetListenSocket(UriHostNameType ipHostNameType)
        {
            if (ipHostNameType == UriHostNameType.IPv4)
            {
                Socket result = ipv4ListenSocket;
                ipv4ListenSocket = null;
                return result;
            }
            else // UriHostNameType.IPv6
            {
                Socket result = ipv6ListenSocket;
                ipv6ListenSocket = null;
                return result;
            }
        }

        public override string Scheme
        {
            get { return Uri.UriSchemeNetTcp; }
        }

        internal static UriPrefixTable<ITransportManagerRegistration> StaticTransportManagerTable
        {
            get
            {
                return transportManagerTable;
            }
        }

        internal override UriPrefixTable<ITransportManagerRegistration> TransportManagerTable
        {
            get
            {
                return transportManagerTable;
            }
        }

        internal static void FixIpv6Hostname(UriBuilder uriBuilder, Uri originalUri)
        {
            if (originalUri.HostNameType == UriHostNameType.IPv6)
            {
                string ipv6Host = originalUri.DnsSafeHost;
                uriBuilder.Host = string.Concat("[", ipv6Host, "]");
            }
        }

        internal override ITransportManagerRegistration CreateTransportManagerRegistration()
        {
            Uri listenUri = BaseUri;
            UriBuilder builder = new UriBuilder(listenUri.Scheme, listenUri.Host, listenUri.Port);
            TcpChannelListener.FixIpv6Hostname(builder, listenUri);
            listenUri = builder.Uri;

            return CreateTransportManagerRegistration(listenUri);
        }

        internal override ITransportManagerRegistration CreateTransportManagerRegistration(Uri listenUri)
        {
            return new ExclusiveTcpTransportManagerRegistration(listenUri, this);
        }

        Socket ListenAndBind(IPEndPoint localEndpoint)
        {
            Socket result = new Socket(localEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                result.Bind(localEndpoint);
            }
            catch (SocketException socketException)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    SocketConnectionListener.ConvertListenException(socketException, localEndpoint));
            }

            return result;
        }

        void SetupUniquePort(BindingContext context)
        {
            IPAddress ipv4Address = IPAddress.Any;
            IPAddress ipv6Address = IPAddress.IPv6Any;

            bool useIPv4 = Socket.OSSupportsIPv4;
            bool useIPv6 = Socket.OSSupportsIPv6;
            if (Uri.HostNameType == UriHostNameType.IPv6)
            {
                useIPv4 = false;
                ipv6Address = IPAddress.Parse(Uri.DnsSafeHost);
            }
            else if (Uri.HostNameType == UriHostNameType.IPv4)
            {
                useIPv6 = false;
                ipv4Address = IPAddress.Parse(Uri.DnsSafeHost);
            }

            if (!useIPv4 && !useIPv6)
            {
                if (Uri.HostNameType == UriHostNameType.IPv6)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                        "context",
                        SR.Format(SR.TcpV6AddressInvalid, Uri));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(
                        "context",
                        SR.Format(SR.TcpV4AddressInvalid, Uri));
                }
            }

            UriBuilder uriBuilder = new UriBuilder(context.ListenUriBaseAddress);
            int port = -1;
            if (!useIPv6) // we just want IPv4
            {
                ipv4ListenSocket = ListenAndBind(new IPEndPoint(ipv4Address, 0));
                port = ((IPEndPoint)ipv4ListenSocket.LocalEndPoint).Port;
            }
            else if (!useIPv4) // or just IPv6
            {
                ipv6ListenSocket = ListenAndBind(new IPEndPoint(ipv6Address, 0));
                port = ((IPEndPoint)ipv6ListenSocket.LocalEndPoint).Port;
            }
            else
            {
                // We need both IPv4 and IPv6 on the same port. We can't atomically bind for IPv4 and IPv6, 
                // so we try 10 times, which even with a 50% failure rate will statistically succeed 99.9% of the time.
                //
                // We look in the range of 49152-65534 for Vista default behavior parity.
                // http://www.iana.org/assignments/port-numbers
                // 
                // We also grab the 10 random numbers in a row to reduce collisions between multiple people somehow
                // colliding on the same seed.
                const int retries = 10;
                const int lowWatermark = 49152;
                const int highWatermark = 65535;

                int[] portNumbers = new int[retries];
                lock (randomPortGenerator)
                {
                    for (int i = 0; i < retries; i++)
                    {
                        portNumbers[i] = randomPortGenerator.Next(lowWatermark, highWatermark);
                    }
                }


                for (int i = 0; i < retries; i++)
                {
                    port = portNumbers[i];
                    try
                    {
                        ipv4ListenSocket = ListenAndBind(new IPEndPoint(ipv4Address, port));
                        ipv6ListenSocket = ListenAndBind(new IPEndPoint(ipv6Address, port));
                        break;
                    }
                    catch (AddressAlreadyInUseException e)
                    {
                        DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                        if (ipv4ListenSocket != null)
                        {
                            ipv4ListenSocket.Close();
                            ipv4ListenSocket = null;
                        }
                        ipv6ListenSocket = null;
                    }
                }

                if (ipv4ListenSocket == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new AddressAlreadyInUseException(SR.Format(SR.UniquePortNotAvailable)));
                }
            }

            uriBuilder.Port = port;
            base.SetUri(uriBuilder.Uri, context.ListenUriRelativeAddress);
        }
    }
}
